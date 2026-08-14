namespace OpdaDemoBff.Models;

// Lifecycle: created "pending" by POST /demo-api/wallet/presentation-request,
// updated to "verified" or "failed" by POST /demo-api/wallet/callback.
public record WalletPresentation(
    string State,
    string TransactionDid,
    IReadOnlyList<string> CredentialTypes,
    string Nonce,
    string Status,
    string CreatedAt,
    long Ttl,
    IReadOnlyList<VerifiedCredential>? Credentials = null,
    string? FailureReason = null,
    string? VerifiedAt = null
);

// One disclosed SD-JWT VC. SignatureVerified is false whenever the issuer isn't
// in the trusted-issuer registry — see IIssuerKeyResolver — so a caller can never
// mistake "we parsed it" for "we verified who issued it".
public record VerifiedCredential(
    string? CredentialType,
    string? Issuer,
    string? Kid,
    bool SignatureVerified,
    bool HolderBindingPresent,
    bool HolderBindingVerified,
    IReadOnlyDictionary<string, string> DisclosedClaims
);
