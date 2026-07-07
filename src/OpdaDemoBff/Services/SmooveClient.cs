using System.Net.Http.Json;
using System.Text.Json;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using Microsoft.Extensions.Logging;

namespace OpdaDemoBff.Services;

// Smoove outbound client — API key only (no mTLS, no private_key_jwt).
// Used exclusively to trigger simulation endpoints that fire webhooks back to /webhook.
public sealed class SmooveClient : ISmooveClient, IDisposable
{
    // Must match the subscription created for this stack (see smoove-direct/subscribe.bru).
    private const string ParticipantName = "OPDA Sandbox";
    private static readonly string[] SubscribedEvents = ["completion_set", "completion_actioned", "tid"];

    private readonly HttpClient _http;
    private readonly ILogger<SmooveClient> _log;
    private readonly string _callbackUrl;

    public static async Task<SmooveClient> CreateAsync(string baseUrl, string apiKeyPath, string callbackUrl, ILogger<SmooveClient> log)
    {
        using var ssm = new AmazonSimpleSystemsManagementClient();
        var res = await ssm.GetParameterAsync(new GetParameterRequest
            { Name = apiKeyPath, WithDecryption = true });

        var http = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + '/') };
        http.DefaultRequestHeaders.Add("X-Api-Key", res.Parameter.Value);
        return new SmooveClient(http, callbackUrl, log);
    }

    internal SmooveClient(HttpClient http, string callbackUrl, ILogger<SmooveClient> log)
    {
        _http        = http;
        _callbackUrl = callbackUrl;
        _log         = log;
    }

    // Smoove periodically clears down ALL subscriptions on its side, which silently
    // kills the webhook flow. Called before each simulate trigger: if no subscription
    // with our callback URL exists, re-create it. A failed LIST is treated as
    // "unknown, proceed" so a transient error can't block a trigger that would
    // otherwise work; only a failed re-subscribe reports failure.
    public async Task<bool> EnsureSubscribedAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_callbackUrl))
            return true; // check disabled — no callback URL configured

        JsonElement subs;
        try
        {
            using var res = await _http.GetAsync("internal/subscriptions", ct);
            if (!res.IsSuccessStatusCode)
            {
                _log.LogWarning("Smoove internal/subscriptions returned {Status} — skipping subscription check",
                    (int)res.StatusCode);
                return true;
            }
            subs = await res.Content.ReadFromJsonAsync<JsonElement>(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException ||
                                   (ex is OperationCanceledException && !ct.IsCancellationRequested))
        {
            _log.LogWarning(ex, "Smoove internal/subscriptions unreachable — skipping subscription check");
            return true;
        }

        if (subs.ValueKind == JsonValueKind.Array &&
            subs.EnumerateArray().Any(s =>
                s.TryGetProperty("callbackUrl", out var cb) && cb.GetString() == _callbackUrl))
            return true;

        _log.LogWarning("Smoove subscription for {CallbackUrl} missing (cleared down?) — re-subscribing", _callbackUrl);
        return await PostAsync("subscribe", new
        {
            participantName = ParticipantName,
            callbackUrl     = _callbackUrl,
            events          = SubscribedEvents,
        }, ct);
    }

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
