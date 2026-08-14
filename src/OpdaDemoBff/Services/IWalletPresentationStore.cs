using OpdaDemoBff.Models;

namespace OpdaDemoBff.Services;

// Deliberately not IWebhookStore: DynamoWebhookStore.StoreAsync hard-codes
// Smoove's JWT claim shape (data.transactionDid / event) to derive its keys.
// A wallet vp_token doesn't carry our transactionDid at all — that only exists
// because *we* generated it at request time — so the correlation key here is
// the OpenID4VP `state`, not something extracted from the credential.
public interface IWalletPresentationStore
{
    Task CreateAsync(
        string state, string transactionDid, IReadOnlyList<string> credentialTypes, string nonce, CancellationToken ct = default);

    Task<WalletPresentation?> GetAsync(string state, CancellationToken ct = default);

    Task CompleteAsync(string state, WalletVerificationOutcome outcome, CancellationToken ct = default);
}
