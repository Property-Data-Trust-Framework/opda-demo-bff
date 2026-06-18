using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OpdaDemoBff.Models;
using OpdaDemoBff.Services;
using Xunit;

namespace OpdaDemoBff.Tests;

public class WebhookTests : IClassFixture<WebhookTests.TestingFactory>
{
    // JWT whose payload decodes to: { "event": "completion_set", "data": { "transactionDid": "did:web:example.com:transaction:test-001" } }
    private const string TestJwt      = "eyJhbGciOiJSUzI1NiJ9.eyJldmVudCI6ImNvbXBsZXRpb25fc2V0IiwiZGF0YSI6eyJ0cmFuc2FjdGlvbkRpZCI6ImRpZDp3ZWI6ZXhhbXBsZS5jb206dHJhbnNhY3Rpb246dGVzdC0wMDEifX0.signature";
    private const string TestDid      = "did:web:example.com:transaction:test-001";
    private const string TestEvent    = "completion_set";

    private readonly HttpClient      _client;
    private readonly FakeWebhookStore _store;

    public WebhookTests(TestingFactory factory)
    {
        _client = factory.CreateClient();
        _store  = factory.Store;
        _store.Stored.Clear();
    }

    // ── POST /webhook ─────────────────────────────────────────────────────────

    [Fact]
    public async Task PostWebhook_ValidBody_Returns200AndStores()
    {
        var resp = await _client.PostAsync("/webhook",
            new StringContent(TestJwt, Encoding.UTF8, "text/plain"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("stored", body.GetProperty("status").GetString());

        Assert.Contains(_store.Stored, e =>
            e.TransactionDid == TestDid &&
            e.Event          == TestEvent &&
            e.RawBody        == TestJwt);
    }

    [Fact]
    public async Task PostWebhook_EmptyBody_Returns400()
    {
        var countBefore = _store.Stored.Count;

        var resp = await _client.PostAsync("/webhook",
            new StringContent("", Encoding.UTF8, "text/plain"));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Equal(countBefore, _store.Stored.Count);
    }

    [Fact]
    public async Task PostWebhook_QuotedJwt_StripsQuotesBeforeStoring()
    {
        var quoted = $"\"{TestJwt}\"";
        var resp   = await _client.PostAsync("/webhook",
            new StringContent(quoted, Encoding.UTF8, "text/plain"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Contains(_store.Stored, e => e.RawBody == TestJwt);
    }

    // ── GET /demo-api/events/{transactionDid} ─────────────────────────────────

    [Fact]
    public async Task GetEvents_ReturnsEventsForTransactionDid()
    {
        _store.Stored.Add(new WebhookEvent(TestDid, TestEvent, "jwt-1",
            DateTimeOffset.UtcNow.ToString("O"), 0));

        var resp = await _client.GetAsync($"/demo-api/events/{Uri.EscapeDataString(TestDid)}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var events = await resp.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(events);
        Assert.NotEmpty(events);
    }

    [Fact]
    public async Task GetEvents_UnknownDid_ReturnsEmptyList()
    {
        var resp = await _client.GetAsync("/demo-api/events/did:web:unknown");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var events = await resp.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(events);
        Assert.Empty(events);
    }

    // ── GET /demo-api/events/{transactionDid}/{event} ─────────────────────────

    [Fact]
    public async Task GetEvent_KnownKeys_ReturnsEvent()
    {
        _store.Stored.Add(new WebhookEvent(TestDid, TestEvent, "jwt-body",
            DateTimeOffset.UtcNow.ToString("O"), 0));

        var resp = await _client.GetAsync(
            $"/demo-api/events/{Uri.EscapeDataString(TestDid)}/{TestEvent}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("jwt-body", body.GetProperty("rawBody").GetString());
    }

    [Fact]
    public async Task GetEvent_UnknownKeys_Returns404()
    {
        var resp = await _client.GetAsync(
            $"/demo-api/events/{Uri.EscapeDataString(TestDid)}/no-such-event");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ── GET /demo-api/health ──────────────────────────────────────────────────

    [Fact]
    public async Task GetHealth_Returns200()
    {
        var resp = await _client.GetAsync("/demo-api/health");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    public class TestingFactory : WebApplicationFactory<Program>
    {
        public FakeWebhookStore Store { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
                services.AddSingleton<IWebhookStore>(Store));
        }
    }

    public class FakeWebhookStore : IWebhookStore
    {
        public List<WebhookEvent> Stored { get; } = [];

        public Task StoreAsync(string rawBody, CancellationToken ct = default)
        {
            var (transactionDid, eventType) = DecodeJwtClaims(rawBody);
            Stored.Add(new WebhookEvent(transactionDid, eventType, rawBody,
                DateTimeOffset.UtcNow.ToString("O"), 0));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<WebhookEvent>> ListAsync(string transactionDid, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<WebhookEvent>>(
                Stored.Where(e => e.TransactionDid == transactionDid).ToList());

        public Task<WebhookEvent?> GetAsync(string transactionDid, string eventType, CancellationToken ct = default) =>
            Task.FromResult(Stored.FirstOrDefault(e =>
                e.TransactionDid == transactionDid && e.Event == eventType));

        private static (string transactionDid, string eventType) DecodeJwtClaims(string rawJwt)
        {
            var parts  = rawJwt.Split('.');
            var base64 = parts[1].Replace('-', '+').Replace('_', '/');
            var padded  = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
            using var doc  = JsonDocument.Parse(Convert.FromBase64String(padded));
            var root = doc.RootElement;
            return (
                root.GetProperty("data").GetProperty("transactionDid").GetString()!,
                root.GetProperty("event").GetString()!
            );
        }
    }
}
