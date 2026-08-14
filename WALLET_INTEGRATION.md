# Wallet Integration Guide — Presenting Credentials to opda-demo-bff

This is the integration guide for a **wallet provider** connecting to the
OpenID4VP endpoints added in ADR-0013 (`opda-ops.wiki`). It's written for an
external third party's dev team, not for us — see `ADR-0013-wallet-credential-verification.md`
for the design reasoning and `verifiable_credentials_scope.md` (sandbox root) for
the underlying credential scenarios this exists to demo.

## Who's who

| Party | Role | Integrates with us? |
|---|---|---|
| **opda-demo-bff** (us) | OpenID4VP **verifier** — requests a presentation, checks it, hands the outcome to our SPA | — |
| **Wallet** (you) | Holds the customer's credentials, responds to our presentation request | **Yes — this document is for you** |
| **Issuer** (e.g. a lender) | Signed the credential(s) the wallet holds | No — the issuer never calls us. Their public key has to reach our trusted-issuer registry by a side channel (see Prerequisites). Issuing credentials into the wallet in the first place (OpenID4VCI) is a separate, currently unbuilt flow — not covered here. |

We only ever request a **presentation** of a credential you already hold. We do
not issue anything to you.

## Prerequisites (one-time, before any presentation will verify)

Every presentation is checked against a trusted-issuer registry — a JSON map of
issuer (`iss`, an HTTPS URL) to PEM-encoded RSA public key, held in one SSM
parameter (`WALLET_TRUSTED_ISSUERS_PATH`). **This fails closed**: if the issuer of
a presented credential isn't in that map, the credential still parses but
verification returns `false`.

Before integration testing can succeed:

1. Whoever operates the credential issuer supplies us with the issuer's `iss`
   URL and RSA public key (PEM).
2. We add that entry to the registry (`terraform/ssm.tf` → `wallet_trusted_issuers`
   Terraform variable → SSM parameter) and redeploy.
3. Only RS256 is accepted. If your issuer signs with anything else (ES256,
   EdDSA, ...), presentations from it will never verify against this BFF as it
   stands today.

## The flow, step by step

```
  SPA/BFF                          Wallet                         opda-demo-bff
     │                                │                                 │
  1. │──POST presentation-request────────────────────────────────────►  │
     │◄─────────────────────────── { state, requestUri } ───────────────│
  2. │──renders requestUri as QR/deep link──►│                          │
  3. │                                │──GET requestUri (= /wallet/request/{state})──►│
     │                                │◄──────── OpenID4VP request object ────────────│
  4. │                                │  (holder reviews & consents)    │
  5. │                                │──POST /wallet/callback (vp_token)────────────►│
     │                                │◄──────────── { status } ──────────────────────│
  6. │──GET /wallet/result/{state}───────────────────────────────────►  │
     │◄──────── verified/failed + disclosed claims ──────────────────── │
```

### 1. We create the presentation request

`POST /demo-api/wallet/presentation-request/{transactionDid}`

```json
// Request body
{ "credentialTypes": ["mortgage-offer", "person-identity"] }
```
```json
// 200 response
{ "state": "a1b2c3d4e5f6...", "requestUri": "https://<bff-host>/demo-api/wallet/request/a1b2c3d4e5f6..." }
```

We render `requestUri` as a QR code / deep link. This step is entirely ours —
included here so you know where the URL your wallet fetches next comes from.

### 2–3. Your wallet fetches the request object

`GET /demo-api/wallet/request/{state}` (the `requestUri` above)

```json
{
  "response_type": "vp_token",
  "response_mode": "direct_post",
  "client_id": "opda-demo-bff",
  "response_uri": "https://<bff-host>/demo-api/wallet/callback",
  "nonce": "9f8e7d6c...",
  "state": "a1b2c3d4e5f6...",
  "credential_types": ["mortgage-offer", "person-identity"]
}
```

`credential_types` is coarse-grained on purpose — match it against the `vct`
values of credentials you hold. There is no claim-level `presentation_definition`
to parse (Attachment A doesn't require selective disclosure requests for the
MVP). A `404` here means the `state` is unknown or the presentation has already
completed — don't retry with a stale QR code.

**Record `nonce`** — it must come back inside your Key Binding JWT if you send
one (see Holder binding, below).

### 4. Holder reviews and consents

Entirely wallet-side. Not our concern beyond: whatever the holder consents to
becomes the `vp_token` in the next step.

### 5. Your wallet posts the presentation

`POST /demo-api/wallet/callback` — **form-encoded**, not JSON:

```
Content-Type: application/x-www-form-urlencoded

state=a1b2c3d4e5f6...&vp_token=<sd-jwt-vc-string>
```

`vp_token` is either:
- a single SD-JWT VC string, or
- a JSON array of SD-JWT VC strings (URL-encoded), for a multi-credential
  presentation like Attachment A Scenario C (offer + identity together)

```json
// 200 response
{ "status": "verified" }
// or
{ "status": "failed" }
```

`400` if `state` or `vp_token` is missing; `404` if `state` is unknown.
A `200` with `"status": "failed"` is not an error response — it means the
request was well-formed but the credential(s) didn't verify. See Failure modes.

### 6. Result pickup

`GET /demo-api/wallet/result/{state}` — polled by our SPA, not by you, but
documented here so you can reproduce what we'll see:

```json
{
  "state": "a1b2c3d4e5f6...",
  "transactionDid": "did:web:example.com:transaction:...",
  "credentialTypes": ["mortgage-offer", "person-identity"],
  "status": "verified",
  "credentials": [
    {
      "credentialType": "mortgage-offer",
      "issuer": "https://credentials.example/mortgage-offer",
      "kid": null,
      "signatureVerified": true,
      "holderBindingPresent": false,
      "holderBindingVerified": false,
      "disclosedClaims": { "loan_amount": "150000", "borrower_given_name": "Robert" }
    }
  ],
  "failureReason": null,
  "verifiedAt": "2026-08-13T10:04:22Z"
}
```

## The SD-JWT VC your wallet needs to send

Wire format: `<issuer-signed JWT>~<disclosure>~<disclosure>~...~[Key Binding JWT]`

**Issuer-signed JWT** (header.payload.signature):
- header: `{ "alg": "RS256", "typ": "JWT" }` — RS256 only, see Prerequisites
- payload must include:
  - `iss` — the issuer's HTTPS URL, exactly as registered with us
  - `_sd_alg`: `"sha-256"` — the only value we support
  - `_sd`: a JSON array of digests, one per disclosed claim
  - `vct` — the credential type, matched against what we display but not (yet — see Known limitations) enforced against what was requested

**Each disclosure**: base64url of `["<salt>", "<claim name>", "<claim value>"]`.
The digest in `_sd` is `base64url(SHA-256(ASCII(<the disclosure string itself>)))`
— computed over the disclosure exactly as it appears on the wire, not over the
decoded JSON.

**Claim names we look for** (per Attachment A — used for the cross-credential
correlation check when presenting more than one credential together):
`given_name`/`family_name`/`date_of_birth` (Person Identity Credential) and
`borrower_given_name`/`borrower_family_name`/`borrower_date_of_birth` (Mortgage
Offer Credential). If two credentials are presented together and any of these
values differ between them, the whole presentation fails — this is how we check
the offer and the identity credential belong to the same person.

**Key Binding JWT** (optional): if present, must contain a `nonce` claim equal
to the `nonce` we issued in step 3. We do not currently check `aud` or `sd_hash`
— see Known limitations.

A complete worked example (building one of these from scratch, correctly signed)
is in `tests/OpdaDemoBff.Tests/SdJwtWalletVerifierTests.cs` — `BuildCredential()`
is the reference for the exact bytes we expect.

## Failure modes

| Symptom | Cause |
|---|---|
| `signatureVerified: false` on every credential | Issuer not in our trusted-issuer registry, or you signed with something other than RS256 — see Prerequisites |
| Credential parses but `TamperedDisclosures` non-empty (visible in logs, not the API response) | A disclosure's digest doesn't match anything in `_sd` — check your digest computation is over the disclosure string, not the decoded JSON |
| `failureReason: "issued-to subject (name + date of birth) does not match..."` | Presenting an offer + identity credential together where the borrower_*/*_name or dob claims differ |
| `404` on `/wallet/request/{state}` | Either the state never existed, or the presentation already completed — request a fresh one |
| `_sd_alg 'xxx' is not supported` (thrown, surfaces as a parse failure) | We only support `sha-256` |

## Known limitations — read before integration testing

- **Credential type isn't enforced against the request.** `credential_types` in
  the request object is informational; nothing today rejects a presentation
  whose `vct` doesn't match what was asked for. Don't rely on us to reject the
  wrong credential type — check `credentialType` in the result yourself for now.
- **Holder binding is nonce-only.** `aud` and `sd_hash` are not checked, so a
  Key Binding JWT is a weaker anti-replay guarantee here than the full spec
  provides.
- **No real trust registry.** The issuer registry is a flat SSM parameter we
  update manually — there's no OpenID Federation, no dynamic issuer discovery.
- **Untested against any real wallet.** Everything above is derived from the
  OpenID4VP spec and our own SD-JWT VC parser, not from interop testing. If
  something here doesn't work against your wallet, the request/response shape
  is the first thing to suspect, not necessarily your implementation.
- **No revocation checking.** Matches Attachment A — out of scope for the demo.

## Reference

- `opda-ops.wiki/ADR-0013-wallet-credential-verification.md` — design record
- `verifiable_credentials_scope.md` (sandbox root) — the credential scenarios and Attachment A formats this demo targets
- `src/OpdaDemoBff/Services/SdJwtVc.cs` — the parser
- `src/OpdaDemoBff/Services/SdJwtWalletVerifier.cs` — the verification logic
- `src/OpdaDemoBff/Program.cs` — the four endpoints, `/demo-api/wallet/*`
