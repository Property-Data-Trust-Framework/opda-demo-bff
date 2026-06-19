using System.Net.Http.Headers;
using System.Net.Http.Json;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Microsoft.Extensions.Logging;

namespace OpdaDemoBff.Services;

// Smoove outbound client — API key only (no mTLS, no private_key_jwt).
// Used exclusively to trigger simulation endpoints that fire webhooks back to /webhook.
public sealed class SmooveClient : ISmooveClient, IDisposable
{
    private readonly HttpClient _http;
    private readonly ILogger<SmooveClient> _log;

    public static async Task<SmooveClient> CreateAsync(string baseUrl, string apiKeyPath, ILogger<SmooveClient> log)
    {
        using var ssm = new AmazonSimpleSystemsManagementClient();
        var res = await ssm.GetParameterAsync(new GetParameterRequest
            { Name = apiKeyPath, WithDecryption = true });

        var http = new HttpClient { BaseAddress = new Uri(baseUrl) };
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", res.Parameter.Value);
        return new SmooveClient(http, log);
    }

    private SmooveClient(HttpClient http, ILogger<SmooveClient> log) { _http = http; _log = log; }

    public async Task<bool> PostAsync(string path, object? body = null, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, path);
        if (body is not null)
            req.Content = JsonContent.Create(body);
        var res = await _http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
        {
            var detail = await res.Content.ReadAsStringAsync(ct);
            _log.LogError("Smoove {Path} returned {Status}: {Body}", path, (int)res.StatusCode, detail);
        }
        return res.IsSuccessStatusCode;
    }

    public void Dispose() => _http.Dispose();
}
