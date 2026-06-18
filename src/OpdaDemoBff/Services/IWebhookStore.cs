using OpdaDemoBff.Models;

namespace OpdaDemoBff.Services;

public interface IWebhookStore
{
    Task StoreAsync(string rawBody, CancellationToken ct = default);
    Task<IReadOnlyList<WebhookEvent>> ListAsync(string transactionDid, CancellationToken ct = default);
    Task<WebhookEvent?> GetAsync(string transactionDid, string eventType, CancellationToken ct = default);
}
