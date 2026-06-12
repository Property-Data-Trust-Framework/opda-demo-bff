using System.Text.Json;

namespace OpdaDemoBff.Services;

public interface IOpdaClient
{
    Task<JsonElement?> GetAsync(string path, CancellationToken ct = default);
    Task<JsonElement?> PostAsync(string path, object body, CancellationToken ct = default);
    Task<JsonElement?> PostFormAsync(string path, IEnumerable<KeyValuePair<string, string>> fields, CancellationToken ct = default);
    // Variants that also capture the x-jws-signature response header (PDI detached-JWS pattern).
    Task<(JsonElement? Body, string? JwsSignature)> GetWithJwsAsync(string path, CancellationToken ct = default);
    Task<(JsonElement? Body, string? JwsSignature)> PostWithJwsAsync(string path, object body, CancellationToken ct = default);
}
