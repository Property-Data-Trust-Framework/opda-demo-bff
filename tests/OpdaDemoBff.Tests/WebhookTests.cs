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
    private readonly HttpClient _client;
    private readonly FakeWebhookStore _store;

    public WebhookTests(TestingFactory factory)
    {
        _client = factory.CreateClient();
        _store   = factory.Store;
    }

    // ── POST /webhook ─────────────────────────────────────────────────────────

    [Fact]
    public async Task PostWebhook_ValidBody_Returns200AndStores()
    {
        var jwt  = "eyJhbGciOiJSUzI1NiJ9.eyJldmVudCI6ImNvbXBsZXRpb25fc2V0In0.signature";
        var resp = await _client.PostAsync("/webhook",
            new StringContent(jwt, Encoding.UTF8, "text/plain"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("eventId").GetString()?.Length > 0);
        Assert.Equal("stored", body.GetProperty("status").GetString());

        Assert.Single(_store.Stored);
        Assert.Equal(jwt, _store.Stored[0].RawBody);
    }

    [Fact]
    public async Task PostWebhook_EmptyBody_Returns400()
    {
        var resp = await _client.PostAsync("/webhook",
            new StringContent("", Encoding.UTF8, "text/plain"));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Empty(_store.Stored);
    }

    // ── GET /demo-api/events ──────────────────────────────────────────────────

    [Fact]
    public async Task GetEvents_ReturnsStoredEvents()
    {
        _store.Stored.Add(new WebhookEvent("id-1", "jwt-1",
            DateTimeOffset.UtcNow.ToString("O"), 0));

        var resp = await _client.GetAsync("/demo-api/events");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var events = await resp.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(events);
        Assert.NotEmpty(events);
    }

    // ── GET /demo-api/events/{id} ─────────────────────────────────────────────

    [Fact]
    public async Task GetEvent_KnownId_ReturnsEvent()
    {
        _store.Stored.Add(new WebhookEvent("known-id", "jwt-body",
            DateTimeOffset.UtcNow.ToString("O"), 0));

        var resp = await _client.GetAsync("/demo-api/events/known-id");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("jwt-body", body.GetProperty("rawBody").GetString());
    }

    [Fact]
    public async Task GetEvent_UnknownId_Returns404()
    {
        var resp = await _client.GetAsync("/demo-api/events/no-such-id");
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
            builder.UseSetting("TABLE_NAME", "test-table");
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IWebhookStore>(Store);
            });
        }
    }

    public class FakeWebhookStore : IWebhookStore
    {
        public List<WebhookEvent> Stored { get; } = [];

        public Task<string> StoreAsync(string rawBody, CancellationToken ct = default)
        {
            var id = Guid.NewGuid().ToString();
            Stored.Add(new WebhookEvent(id, rawBody, DateTimeOffset.UtcNow.ToString("O"), 0));
            return Task.FromResult(id);
        }

        public Task<IReadOnlyList<WebhookEvent>> ListAsync(int limit = 50, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<WebhookEvent>>(Stored.TakeLast(limit).Reverse().ToList());

        public Task<WebhookEvent?> GetAsync(string eventId, CancellationToken ct = default) =>
            Task.FromResult(Stored.FirstOrDefault(e => e.EventId == eventId));
    }
}
