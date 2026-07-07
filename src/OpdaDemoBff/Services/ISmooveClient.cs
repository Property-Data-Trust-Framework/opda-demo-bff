namespace OpdaDemoBff.Services;

public interface ISmooveClient
{
    /// Verifies our webhook subscription still exists on Smoove's side (they
    /// periodically clear all subscriptions down) and re-subscribes if not.
    /// Returns false only when the subscription is known-missing AND
    /// re-subscribing failed.
    Task<bool> EnsureSubscribedAsync(CancellationToken ct = default);

    Task<bool> PostAsync(string path, object? body = null, CancellationToken ct = default);
}
