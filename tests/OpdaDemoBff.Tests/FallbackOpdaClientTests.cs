using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using OpdaDemoBff.Services;
using Xunit;

namespace OpdaDemoBff.Tests;

public class FallbackOpdaClientTests
{
    private static FallbackOpdaClient OverNull() =>
        new(new NullOpdaClient(), NullLogger<FallbackOpdaClient>.Instance);

    [Theory]
    [InlineData("/v1/places/find?query=elm+grove", "data")]
    [InlineData("/v1/uprn/validate/000005114578", "data")]
    [InlineData("/v1/epc/000005114578?schema=pdtf-v3.5", "data")]
    [InlineData("/v1/council-tax/000005114578", "data")]
    [InlineData("/v1/coalfield/000005114578", "data")]
    [InlineData("/v1/documents/000005114578", "data")]
    [InlineData("/v1/source-of-funds/abc-123", "data")]
    [InlineData("/api/v1/opda/chains", "data")]
    [InlineData("/metainformation/v1.0.0/uprn/5114578", "uprn")]
    [InlineData("/property/5114578/available", "uprn")]
    public async Task ServesParseableFixtureWhenInnerReturnsNull(string path, string expectedTopLevelProperty)
    {
        var result = await OverNull().GetAsync(path);

        Assert.NotNull(result);
        Assert.True(result.Value.TryGetProperty(expectedTopLevelProperty, out _),
            $"fixture for {path} missing '{expectedTopLevelProperty}': {result.Value.GetRawText()}");
    }

    [Fact]
    public async Task PackFragmentFixturesAssembleIntoOnePack()
    {
        var client = OverNull();
        var epc = await client.GetAsync("/v1/epc/000005114578");
        var ct  = await client.GetAsync("/v1/council-tax/000005114578");
        var mra = await client.GetAsync("/v1/coalfield/000005114578");
        var lr  = await client.PostAsync("/opda/official-copies/v1/register-extract", new { });

        var pack = PdtfPackAssembler.Assemble(
            ("epc", epc), ("councilTax", ct), ("coalfield", mra), ("titleRegister", lr));

        var pp = pack["propertyPack"]!;
        Assert.NotNull(pp["energyEfficiency"]);
        Assert.NotNull(pp["councilTax"]);
        Assert.NotNull(pp["environmentalIssues"]);
        Assert.NotNull(pp["titlesToBeSold"]);
        // Each fragment carried a provenance block, honest about being a fixture.
        Assert.Equal("disconnected-fixture", (string?)pack["provenance"]!["epc"]!["kid"]);
    }

    [Fact]
    public async Task PassesInnerResultThroughUntouched()
    {
        var marker = JsonSerializer.Deserialize<JsonElement>("""{"live":true}""");
        var client = new FallbackOpdaClient(new FixedOpdaClient(marker), NullLogger<FallbackOpdaClient>.Instance);

        var result = await client.GetAsync("/v1/epc/000005114578");

        Assert.True(result!.Value.GetProperty("live").GetBoolean());
    }

    [Fact]
    public async Task UnknownPathStaysNull()
    {
        Assert.Null(await OverNull().GetAsync("/no/fixture/for/this"));
    }

    [Fact]
    public async Task JwsVariantsServeFixtureWithNullSignature()
    {
        var (body, jws) = await OverNull().PostWithJwsAsync("/opda-opaque/appraisal/v1/property-pack/uprn", new { });

        Assert.NotNull(body);
        Assert.Null(jws);
        Assert.True(body.Value.TryGetProperty("propertyPack", out _));
    }

    private sealed class NullOpdaClient : IOpdaClient
    {
        public Task<JsonElement?> GetAsync(string path, CancellationToken ct = default) => Task.FromResult<JsonElement?>(null);
        public Task<JsonElement?> PostAsync(string path, object body, CancellationToken ct = default) => Task.FromResult<JsonElement?>(null);
        public Task<JsonElement?> PostFormAsync(string path, IEnumerable<KeyValuePair<string, string>> fields, CancellationToken ct = default) => Task.FromResult<JsonElement?>(null);
        public Task<(JsonElement? Body, string? JwsSignature)> GetWithJwsAsync(string path, CancellationToken ct = default) => Task.FromResult<(JsonElement?, string?)>((null, null));
        public Task<(JsonElement? Body, string? JwsSignature)> PostWithJwsAsync(string path, object body, CancellationToken ct = default) => Task.FromResult<(JsonElement?, string?)>((null, null));
    }

    private sealed class FixedOpdaClient(JsonElement value) : IOpdaClient
    {
        public Task<JsonElement?> GetAsync(string path, CancellationToken ct = default) => Task.FromResult<JsonElement?>(value);
        public Task<JsonElement?> PostAsync(string path, object body, CancellationToken ct = default) => Task.FromResult<JsonElement?>(value);
        public Task<JsonElement?> PostFormAsync(string path, IEnumerable<KeyValuePair<string, string>> fields, CancellationToken ct = default) => Task.FromResult<JsonElement?>(value);
        public Task<(JsonElement? Body, string? JwsSignature)> GetWithJwsAsync(string path, CancellationToken ct = default) => Task.FromResult<(JsonElement?, string?)>((value, "jws"));
        public Task<(JsonElement? Body, string? JwsSignature)> PostWithJwsAsync(string path, object body, CancellationToken ct = default) => Task.FromResult<(JsonElement?, string?)>((value, "jws"));
    }
}
