using OpdaDemoBff.Models;

namespace OpdaDemoBff.Services;

public interface IWebhookStore
{
    Task<string> StoreAsync(string rawBody, CancellationToken ct = default);
    Task<IReadOnlyList<WebhookEvent>> ListAsync(int limit = 50, CancellationToken ct = default);
    Task<WebhookEvent?> GetAsync(string eventId, CancellationToken ct = default);
}
