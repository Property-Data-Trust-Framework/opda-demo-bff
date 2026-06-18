namespace OpdaDemoBff.Services;

public interface ISmooveClient
{
    Task<bool> PostAsync(string path, object? body = null, CancellationToken ct = default);
}
