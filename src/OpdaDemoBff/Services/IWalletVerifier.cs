using OpdaDemoBff.Models;

namespace OpdaDemoBff.Services;

public interface IWalletVerifier
{
    Task<WalletVerificationOutcome> VerifyAsync(
        string vpToken,
        string expectedNonce,
        IReadOnlyList<string> requestedCredentialTypes,
        CancellationToken ct = default);
}

public record WalletVerificationOutcome(
    bool Verified,
    string? FailureReason,
    IReadOnlyList<VerifiedCredential> Credentials
);
