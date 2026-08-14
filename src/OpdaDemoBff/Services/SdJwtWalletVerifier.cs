using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using OpdaDemoBff.Models;

namespace OpdaDemoBff.Services;

// Verifies an OpenID4VP vp_token: one SD-JWT VC, or a JSON array of them
// (Scenario C — offer + person-identity presented together, per
// verifiable_credentials_scope.md). Real RS256 signature verification runs
// whenever the issuer is in the trusted-issuer registry; otherwise the
// credential is still parsed and its disclosed claims are still returned, but
// SignatureVerified stays false and the overall presentation fails — a caller
// can inspect what was disclosed without ever mistaking that for trust.
public sealed class SdJwtWalletVerifier(IIssuerKeyResolver issuerKeys, ILogger<SdJwtWalletVerifier> log) : IWalletVerifier
{
    private static readonly JwtSecurityTokenHandler Handler = new();

    public async Task<WalletVerificationOutcome> VerifyAsync(
        string vpToken,
        string expectedNonce,
        IReadOnlyList<string> requestedCredentialTypes,
        CancellationToken ct = default)
    {
        List<string> tokens;
        try
        {
            tokens = vpToken.TrimStart().StartsWith('[')
                ? JsonSerializer.Deserialize<List<string>>(vpToken) ?? []
                : [vpToken];
        }
        catch (JsonException)
        {
            return new WalletVerificationOutcome(false, "vp_token was not an SD-JWT VC or a JSON array of them", []);
        }

        if (tokens.Count == 0)
            return new WalletVerificationOutcome(false, "vp_token contained no credentials", []);

        var credentials = new List<VerifiedCredential>();
        foreach (var token in tokens)
        {
            var outcome = await VerifyOneAsync(token, expectedNonce, ct);
            if (outcome.credential is null)
                return new WalletVerificationOutcome(false, outcome.failure, credentials);
            credentials.Add(outcome.credential);
        }

        // Attachment A correlation: the offer and the person-identity credential
        // are tied together by matching the "issued to" subject (given name,
        // family name, date of birth), not by the same person presenting both.
        var subjectSets = credentials
            .Select(c => (
                Given: c.DisclosedClaims.GetValueOrDefault("given_name") ?? c.DisclosedClaims.GetValueOrDefault("borrower_given_name"),
                Family: c.DisclosedClaims.GetValueOrDefault("family_name") ?? c.DisclosedClaims.GetValueOrDefault("borrower_family_name"),
                Dob: c.DisclosedClaims.GetValueOrDefault("date_of_birth") ?? c.DisclosedClaims.GetValueOrDefault("borrower_date_of_birth")))
            .Where(s => s.Given is not null || s.Family is not null || s.Dob is not null)
            .Distinct()
            .ToList();

        if (subjectSets.Count > 1)
            return new WalletVerificationOutcome(
                false, "issued-to subject (name + date of birth) does not match across the presented credentials", credentials);

        var allSignaturesOk = credentials.All(c => c.SignatureVerified);
        return new WalletVerificationOutcome(
            allSignaturesOk,
            allSignaturesOk ? null : "one or more credentials were not signed by a trusted issuer",
            credentials);
    }

    private async Task<(VerifiedCredential? credential, string? failure)> VerifyOneAsync(
        string token, string expectedNonce, CancellationToken ct)
    {
        SdJwtVcParseResult parsed;
        try
        {
            parsed = SdJwtVc.Parse(token);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "wallet presentation: SD-JWT VC failed to parse");
            return (null, $"credential did not parse: {ex.Message}");
        }

        if (parsed.TamperedDisclosures.Count > 0)
            return (null, "one or more disclosures did not match the issuer's _sd digests");

        var issuer = parsed.IssuerToken.Issuer;
        var kid = parsed.IssuerToken.Header.TryGetValue("kid", out var kidObj) ? kidObj?.ToString() : null;
        var vct = parsed.IssuerToken.Payload.TryGetValue("vct", out var vctObj) ? vctObj?.ToString() : null;

        var key = !string.IsNullOrEmpty(issuer) ? await issuerKeys.ResolveAsync(issuer, ct) : null;
        var signatureVerified = false;
        if (key is not null)
        {
            try
            {
                Handler.ValidateToken(parsed.IssuerJwt, new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = false,
                    IssuerSigningKey = new RsaSecurityKey(key),
                    ValidAlgorithms = ["RS256"],
                }, out _);
                signatureVerified = true;
            }
            catch (SecurityTokenException ex)
            {
                log.LogWarning(ex, "wallet presentation: signature check failed for issuer {Issuer}", issuer);
            }
        }
        else
        {
            log.LogWarning(
                "wallet presentation: issuer {Issuer} is not in the trusted-issuer registry — signature not verified", issuer);
        }

        // Best-effort holder binding: nonce match only. Full RFC 8705-style
        // binding also checks `aud` and `sd_hash` — not implemented here, see
        // ADR-0013's open questions.
        var holderBindingPresent = parsed.KeyBindingJwt is not null;
        var holderBindingVerified = false;
        if (holderBindingPresent)
        {
            var kbToken = Handler.ReadJwtToken(parsed.KeyBindingJwt);
            holderBindingVerified = kbToken.Payload.TryGetValue("nonce", out var n) && n?.ToString() == expectedNonce;
        }

        return (new VerifiedCredential(
            vct, issuer, kid, signatureVerified, holderBindingPresent, holderBindingVerified, parsed.DisclosedClaims), null);
    }
}
