using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using OpdaDemoBff.Services;
using Xunit;

namespace OpdaDemoBff.Tests;

// EnsureSubscribedAsync: Smoove periodically clears down all subscriptions, so
// each trigger first checks ours is still present and re-creates it if not.
public class SmooveClientTests
{
    private const string CallbackUrl = "https://ext.example.org/webhook";

    private const string OurSubscription =
        $$"""[{"id":"abc","participantName":"OPDA Sandbox","callbackUrl":"{{CallbackUrl}}","events":["completion_set","completion_actioned","tid"],"createdAt":"2026-07-06T00:00:00Z"}]""";

    [Fact]
    public async Task SubscriptionPresent_ReturnsTrue_WithoutResubscribing()
    {
        var handler = new FakeHandler(("GET internal/subscriptions", HttpStatusCode.OK, OurSubscription));
        var client  = NewClient(handler, CallbackUrl);

        Assert.True(await client.EnsureSubscribedAsync());
        Assert.DoesNotContain(handler.Requests, r => r.StartsWith("POST"));
    }

    [Fact]
    public async Task SubscriptionMissing_Resubscribes_WithCallbackUrlAndEvents()
    {
        var handler = new FakeHandler(
            ("GET internal/subscriptions", HttpStatusCode.OK, "[]"),
            ("POST subscribe", HttpStatusCode.Created, $$"""{"id":"new-id","callbackUrl":"{{CallbackUrl}}"}"""));
        var client = NewClient(handler, CallbackUrl);

        Assert.True(await client.EnsureSubscribedAsync());

        Assert.Contains("POST subscribe", handler.Requests);
        var body = JsonDocument.Parse(handler.LastBody!).RootElement;
        Assert.Equal(CallbackUrl, body.GetProperty("callbackUrl").GetString());
        Assert.Equal(3, body.GetProperty("events").GetArrayLength());
    }

    [Fact]
    public async Task SubscriptionMissing_OtherSubscribersPresent_StillResubscribes()
    {
        var others = """[{"id":"x","callbackUrl":"https://someone-else.example/webhook"}]""";
        var handler = new FakeHandler(
            ("GET internal/subscriptions", HttpStatusCode.OK, others),
            ("POST subscribe", HttpStatusCode.Created, """{"id":"new-id"}"""));
        var client = NewClient(handler, CallbackUrl);

        Assert.True(await client.EnsureSubscribedAsync());
        Assert.Contains("POST subscribe", handler.Requests);
    }

    [Fact]
    public async Task SubscriptionMissing_ResubscribeFails_ReturnsFalse()
    {
        var handler = new FakeHandler(
            ("GET internal/subscriptions", HttpStatusCode.OK, "[]"),
            ("POST subscribe", HttpStatusCode.InternalServerError, ""));
        var client = NewClient(handler, CallbackUrl);

        Assert.False(await client.EnsureSubscribedAsync());
    }

    [Fact]
    public async Task ListFails_ProceedsWithoutBlocking()
    {
        var handler = new FakeHandler(("GET internal/subscriptions", HttpStatusCode.InternalServerError, ""));
        var client  = NewClient(handler, CallbackUrl);

        Assert.True(await client.EnsureSubscribedAsync());
        Assert.DoesNotContain(handler.Requests, r => r.StartsWith("POST"));
    }

    [Fact]
    public async Task NoCallbackUrlConfigured_CheckIsDisabled()
    {
        var handler = new FakeHandler();
        var client  = NewClient(handler, callbackUrl: "");

        Assert.True(await client.EnsureSubscribedAsync());
        Assert.Empty(handler.Requests);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static SmooveClient NewClient(FakeHandler handler, string callbackUrl) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://smoove.test/opda/") },
            callbackUrl, NullLogger<SmooveClient>.Instance);

    private sealed class FakeHandler(params (string key, HttpStatusCode status, string body)[] responses) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var key = $"{request.Method} {request.RequestUri!.AbsolutePath.Replace("/opda/", "")}";
            Requests.Add(key);
            if (request.Content is not null)
                LastBody = await request.Content.ReadAsStringAsync(ct);

            var (_, status, body) = responses.FirstOrDefault(r => r.key == key);
            if (status == default)
                return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent($"no fake for {key}") };
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
