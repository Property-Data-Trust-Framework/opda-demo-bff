using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OpdaDemoBff.Models;
using OpdaDemoBff.Services;
using Xunit;

namespace OpdaDemoBff.Tests;

public class WalletEndpointsTests : IClassFixture<WalletEndpointsTests.TestingFactory>
{
    private const string TransactionDid = "did:web:example.com:transaction:test-001";

    private readonly HttpClient _client;
    private readonly FakeWalletPresentationStore _store;
    private readonly FakeWalletVerifier _verifier;

    public WalletEndpointsTests(TestingFactory factory)
    {
        _client = factory.CreateClient();
        _store = factory.Store;
        _verifier = factory.Verifier;
        _store.Items.Clear();
    }

    // ── POST /demo-api/wallet/presentation-request/{transactionDid} ─────────

    [Fact]
    public async Task CreatesPendingPresentation_ReturnsStateAndRequestUri()
    {
        var resp = await _client.PostAsJsonAsync(
            $"/demo-api/wallet/presentation-request/{Uri.EscapeDataString(TransactionDid)}",
            new { credentialTypes = new[] { "mortgage-offer" } });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var state = body.GetProperty("state").GetString();
        Assert.False(string.IsNullOrEmpty(state));
        Assert.Contains($"/demo-api/wallet/request/{state}", body.GetProperty("requestUri").GetString());
        Assert.True(_store.Items.ContainsKey(state!));
        Assert.Equal("pending", _store.Items[state!].Status);
    }

    [Fact]
    public async Task EmptyCredentialTypes_Returns400()
    {
        var resp = await _client.PostAsJsonAsync(
            $"/demo-api/wallet/presentation-request/{Uri.EscapeDataString(TransactionDid)}",
            new { credentialTypes = Array.Empty<string>() });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // ── GET /demo-api/wallet/request/{state} ─────────────────────────────────

    [Fact]
    public async Task GetRequest_PendingState_ReturnsOpenId4VpRequestObject()
    {
        _store.Items["state-1"] = new WalletPresentation(
            "state-1", TransactionDid, ["mortgage-offer"], "nonce-1", "pending", DateTimeOffset.UtcNow.ToString("O"), 0);

        var resp = await _client.GetAsync("/demo-api/wallet/request/state-1");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("vp_token", body.GetProperty("response_type").GetString());
        Assert.Equal("direct_post", body.GetProperty("response_mode").GetString());
        Assert.Equal("nonce-1", body.GetProperty("nonce").GetString());
        Assert.Equal("state-1", body.GetProperty("state").GetString());
    }

    [Fact]
    public async Task GetRequest_UnknownState_Returns404()
    {
        var resp = await _client.GetAsync("/demo-api/wallet/request/no-such-state");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task GetRequest_AlreadyCompletedState_Returns404()
    {
        _store.Items["state-done"] = new WalletPresentation(
            "state-done", TransactionDid, ["mortgage-offer"], "nonce", "verified", DateTimeOffset.UtcNow.ToString("O"), 0);

        var resp = await _client.GetAsync("/demo-api/wallet/request/state-done");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ── POST /demo-api/wallet/callback ───────────────────────────────────────

    [Fact]
    public async Task Callback_ValidStateAndToken_VerifiesAndCompletesStore()
    {
        _store.Items["state-2"] = new WalletPresentation(
            "state-2", TransactionDid, ["mortgage-offer"], "nonce-2", "pending", DateTimeOffset.UtcNow.ToString("O"), 0);
        _verifier.NextOutcome = new WalletVerificationOutcome(true, null,
            [new VerifiedCredential("mortgage-offer", "https://issuer.example", null, true, false, false,
                new Dictionary<string, string> { ["loan_amount"] = "150000" })]);

        var resp = await _client.PostAsync("/demo-api/wallet/callback", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["state"] = "state-2", ["vp_token"] = "sd-jwt-vc-goes-here" }));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("verified", body.GetProperty("status").GetString());
        Assert.Equal("verified", _store.Items["state-2"].Status);
        Assert.Equal("sd-jwt-vc-goes-here", _verifier.LastVpToken);
    }

    [Fact]
    public async Task Callback_FailedVerification_StoresFailedStatus()
    {
        _store.Items["state-3"] = new WalletPresentation(
            "state-3", TransactionDid, ["mortgage-offer"], "nonce-3", "pending", DateTimeOffset.UtcNow.ToString("O"), 0);
        _verifier.NextOutcome = new WalletVerificationOutcome(false, "issuer not trusted", []);

        var resp = await _client.PostAsync("/demo-api/wallet/callback", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["state"] = "state-3", ["vp_token"] = "sd-jwt-vc" }));

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("failed", body.GetProperty("status").GetString());
        Assert.Equal("failed", _store.Items["state-3"].Status);
    }

    [Fact]
    public async Task Callback_MissingVpToken_Returns400()
    {
        var resp = await _client.PostAsync("/demo-api/wallet/callback", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["state"] = "state-4" }));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Callback_UnknownState_Returns404()
    {
        var resp = await _client.PostAsync("/demo-api/wallet/callback", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["state"] = "no-such-state", ["vp_token"] = "x" }));

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ── GET /demo-api/wallet/result/{state} ──────────────────────────────────

    [Fact]
    public async Task GetResult_KnownState_ReturnsRecord()
    {
        _store.Items["state-5"] = new WalletPresentation(
            "state-5", TransactionDid, ["mortgage-offer"], "nonce-5", "verified", DateTimeOffset.UtcNow.ToString("O"), 0);

        var resp = await _client.GetAsync("/demo-api/wallet/result/state-5");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("verified", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task GetResult_UnknownState_Returns404()
    {
        var resp = await _client.GetAsync("/demo-api/wallet/result/no-such-state");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    public class TestingFactory : WebApplicationFactory<Program>
    {
        public FakeWalletPresentationStore Store { get; } = new();
        public FakeWalletVerifier Verifier { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IWalletPresentationStore>(Store);
                services.AddSingleton<IWalletVerifier>(Verifier);
            });
        }
    }

    public class FakeWalletPresentationStore : IWalletPresentationStore
    {
        public Dictionary<string, WalletPresentation> Items { get; } = new();

        public Task CreateAsync(
            string state, string transactionDid, IReadOnlyList<string> credentialTypes, string nonce, CancellationToken ct = default)
        {
            Items[state] = new WalletPresentation(
                state, transactionDid, credentialTypes, nonce, "pending", DateTimeOffset.UtcNow.ToString("O"), 0);
            return Task.CompletedTask;
        }

        public Task<WalletPresentation?> GetAsync(string state, CancellationToken ct = default) =>
            Task.FromResult(Items.TryGetValue(state, out var v) ? v : null);

        public Task CompleteAsync(string state, WalletVerificationOutcome outcome, CancellationToken ct = default)
        {
            if (Items.TryGetValue(state, out var existing))
            {
                Items[state] = existing with
                {
                    Status = outcome.Verified ? "verified" : "failed",
                    Credentials = outcome.Credentials,
                    FailureReason = outcome.FailureReason,
                    VerifiedAt = DateTimeOffset.UtcNow.ToString("O"),
                };
            }
            return Task.CompletedTask;
        }
    }

    public class FakeWalletVerifier : IWalletVerifier
    {
        public WalletVerificationOutcome NextOutcome { get; set; } = new(true, null, []);
        public string? LastVpToken { get; private set; }

        public Task<WalletVerificationOutcome> VerifyAsync(
            string vpToken, string expectedNonce, IReadOnlyList<string> requestedCredentialTypes, CancellationToken ct = default)
        {
            LastVpToken = vpToken;
            return Task.FromResult(NextOutcome);
        }
    }
}
