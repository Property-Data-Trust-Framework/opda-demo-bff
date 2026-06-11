using Amazon.DynamoDBv2;
using Microsoft.Extensions.DependencyInjection;
using OpdaDemoBff.Config;
using OpdaDemoBff.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(o => o.TimestampFormat = "yyyy-MM-ddTHH:mm:ssZ");

builder.Services.Configure<DynamoConfig>(
    builder.Configuration.GetSection(nameof(DynamoConfig)));

builder.Services.AddSingleton<IAmazonDynamoDB>(_ => new AmazonDynamoDBClient());
builder.Services.AddSingleton<IWebhookStore, DynamoWebhookStore>();

builder.Services.AddSingleton<IOpdaClient>(_ =>
    OpdaClient.CreateAsync(new OpdaClientConfig
    {
        ApiBaseUrl     = Environment.GetEnvironmentVariable("OPDA_API_BASE_URL") ?? "",
        ClientCertPath = Environment.GetEnvironmentVariable("OPDA_CLIENT_CERT_PATH") ?? "",
        ClientKeyPath  = Environment.GetEnvironmentVariable("OPDA_CLIENT_KEY_PATH") ?? "",
        SigningKeyPath  = Environment.GetEnvironmentVariable("OPDA_SIGNING_KEY_PATH") ?? "",
        ClientId        = Environment.GetEnvironmentVariable("OPDA_CLIENT_ID") ?? "",
        TokenEndpoint   = Environment.GetEnvironmentVariable("OPDA_TOKEN_ENDPOINT") ?? "",
        Scope           = Environment.GetEnvironmentVariable("OPDA_SCOPE") ?? "land-registry",
    }).GetAwaiter().GetResult());

// ViewMyChain — same mTLS cert + signing key, different base URL and scope.
builder.Services.AddKeyedSingleton<IOpdaClient>("vmc", (_, _) =>
    OpdaClient.CreateAsync(new OpdaClientConfig
    {
        ApiBaseUrl     = Environment.GetEnvironmentVariable("VMC_BASE_URL") ?? "",
        ClientCertPath = Environment.GetEnvironmentVariable("OPDA_CLIENT_CERT_PATH") ?? "",
        ClientKeyPath  = Environment.GetEnvironmentVariable("OPDA_CLIENT_KEY_PATH") ?? "",
        SigningKeyPath  = Environment.GetEnvironmentVariable("OPDA_SIGNING_KEY_PATH") ?? "",
        ClientId        = Environment.GetEnvironmentVariable("OPDA_CLIENT_ID") ?? "",
        TokenEndpoint   = Environment.GetEnvironmentVariable("OPDA_TOKEN_ENDPOINT") ?? "",
        Scope           = Environment.GetEnvironmentVariable("VMC_SCOPE") ?? "transaction-status",
    }).GetAwaiter().GetResult());

// Property Deals Insight — same mTLS cert + signing key, scope: property-pack.
builder.Services.AddKeyedSingleton<IOpdaClient>("pdi", (_, _) =>
    OpdaClient.CreateAsync(new OpdaClientConfig
    {
        ApiBaseUrl     = Environment.GetEnvironmentVariable("PDI_BASE_URL") ?? "",
        ClientCertPath = Environment.GetEnvironmentVariable("OPDA_CLIENT_CERT_PATH") ?? "",
        ClientKeyPath  = Environment.GetEnvironmentVariable("OPDA_CLIENT_KEY_PATH") ?? "",
        SigningKeyPath  = Environment.GetEnvironmentVariable("OPDA_SIGNING_KEY_PATH") ?? "",
        ClientId        = Environment.GetEnvironmentVariable("OPDA_CLIENT_ID") ?? "",
        TokenEndpoint   = Environment.GetEnvironmentVariable("OPDA_TOKEN_ENDPOINT") ?? "",
        Scope           = Environment.GetEnvironmentVariable("PDI_SCOPE") ?? "property-pack",
    }).GetAwaiter().GetResult());

// Sprift — base URL and scope TBD pending confirmation of their PDTF sandbox endpoint.
// Skipped if SPRIFT_BASE_URL is not configured.
var spriftBaseUrl = Environment.GetEnvironmentVariable("SPRIFT_BASE_URL") ?? "";
if (!string.IsNullOrEmpty(spriftBaseUrl))
{
    builder.Services.AddKeyedSingleton<IOpdaClient>("sprift", (_, _) =>
        OpdaClient.CreateAsync(new OpdaClientConfig
        {
            ApiBaseUrl     = spriftBaseUrl,
            ClientCertPath = Environment.GetEnvironmentVariable("OPDA_CLIENT_CERT_PATH") ?? "",
            ClientKeyPath  = Environment.GetEnvironmentVariable("OPDA_CLIENT_KEY_PATH") ?? "",
            SigningKeyPath  = Environment.GetEnvironmentVariable("OPDA_SIGNING_KEY_PATH") ?? "",
            ClientId        = Environment.GetEnvironmentVariable("OPDA_CLIENT_ID") ?? "",
            TokenEndpoint   = Environment.GetEnvironmentVariable("OPDA_TOKEN_ENDPOINT") ?? "",
            Scope           = Environment.GetEnvironmentVariable("SPRIFT_SCOPE") ?? "",
        }).GetAwaiter().GetResult());
}

var app = builder.Build();

// ── POST /webhook ─────────────────────────────────────────────────────────────
// Smoove delivers a signed RS256 JWT as the raw request body.
// We store it as-is for now to inspect the shape before adding JWT validation.

app.MapPost("/webhook", async (HttpRequest request, IWebhookStore store) =>
{
    using var reader = new StreamReader(request.Body);
    var rawBody = await reader.ReadToEndAsync();

    if (string.IsNullOrWhiteSpace(rawBody))
        return Results.BadRequest(new { error = "Empty body" });

    var eventId = await store.StoreAsync(rawBody);
    return Results.Ok(new { eventId, status = "stored" });
});

// ── GET /demo-api/events ──────────────────────────────────────────────────────

app.MapGet("/demo-api/events", async (IWebhookStore store, int limit = 50) =>
    Results.Ok(await store.ListAsync(Math.Min(limit, 100))));

// ── GET /demo-api/events/{id} ─────────────────────────────────────────────────

app.MapGet("/demo-api/events/{eventId}", async (string eventId, IWebhookStore store) =>
{
    var evt = await store.GetAsync(eventId);
    return evt is not null ? Results.Ok(evt) : Results.NotFound();
});

// ── GET /demo-api/health ──────────────────────────────────────────────────────

app.MapGet("/demo-api/health", () => Results.Ok(new { status = "healthy" }));

// ── GET /demo-api/address?q= ──────────────────────────────────────────────────
// Proxies OS Places. SPA uses this for the address search box.

app.MapGet("/demo-api/address", async (string q, IOpdaClient opda) =>
{
    var result = await opda.GetAsync($"/v1/places/find?query={Uri.EscapeDataString(q)}&maxresults=5");
    return result is not null ? Results.Ok(result) : Results.StatusCode(502);
});

// ── GET /demo-api/uprn/{uprn} ─────────────────────────────────────────────────
// Proxies UPRN Validator. Auto-fires after the agent invites the seller.

app.MapGet("/demo-api/uprn/{uprn}", async (string uprn, IOpdaClient opda) =>
{
    var result = await opda.GetAsync($"/v1/uprn/validate/{uprn}");
    return result is not null ? Results.Ok(result) : Results.StatusCode(502);
});

// ── GET /demo-api/pack/{uprn} ─────────────────────────────────────────────────
// Fans out to EPC + Council Tax + Coalfield + LR Facade in parallel.
// titleNumber: demo default EXC10010 — pass ?titleNumber=... to override.

app.MapGet("/demo-api/pack/{uprn}", async (
    string uprn,
    string? titleNumber,
    IOpdaClient opda,
    CancellationToken ct) =>
{
    var tn = string.IsNullOrEmpty(titleNumber) ? "EXC10010" : titleNumber;

    var lrBody = new
    {
        messageId = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
        externalReference = "demo-bff",
        customerReference = "demo-bff",
        titleNumber = tn,
        expectedPrice = 10,
        titleKnownOfficialCopy = new
        {
            continueIfTitleIsClosedAndContinued = false,
            notifyIfPendingFirstRegistration    = false,
            notifyIfPendingApplication          = false,
            sendBackDated                       = false,
            continueIfActualFeeExceedsExpected  = true,
            includeTitlePlanIndicator           = false,
        }
    };

    var epcTask = opda.GetAsync($"/v1/epc/{uprn}", ct);
    var ctTask  = opda.GetAsync($"/v1/council-tax/{uprn}", ct);
    var mraTask = opda.GetAsync($"/v1/coalfield/{uprn}", ct);
    var lrTask  = opda.PostAsync("/opda/official-copies/v1/register-extract", lrBody, ct);

    await Task.WhenAll(epcTask, ctTask, mraTask, lrTask);

    return Results.Ok(new
    {
        epc           = epcTask.Result,
        councilTax    = ctTask.Result,
        coalfield     = mraTask.Result,
        titleRegister = lrTask.Result,
    });
});

// ── GET /demo-api/surveys/{uprn} ──────────────────────────────────────────────
// Proxies Survey Shack. Used by sconv and bconv retrieve-surveys nodes.

app.MapGet("/demo-api/surveys/{uprn}", async (string uprn, IOpdaClient opda) =>
{
    var result = await opda.GetAsync($"/v1/documents/{uprn}");
    return result is not null ? Results.Ok(result) : Results.StatusCode(502);
});

// ── POST /demo-api/source-of-funds ────────────────────────────────────────────
// Proxies Armalytix. BFF generates a clientRequestId on behalf of the SPA.

app.MapPost("/demo-api/source-of-funds", async (IOpdaClient opda) =>
{
    var clientRequestId = Guid.NewGuid().ToString("N")[..12];
    var result = await opda.GetAsync($"/v1/source-of-funds/{clientRequestId}");
    return result is not null ? Results.Ok(result) : Results.StatusCode(502);
});

// ── GET /demo-api/pdi/pack/{uprn} ────────────────────────────────────────────
// Full PDTF v3.5.0 property pack from Property Deals Insight.
// Optional query params mirror the PDI request body: propertyType, internalAreaSqM,
// bedrooms, bathrooms, receptions. All are auto-enriched by PDI when omitted.

app.MapGet("/demo-api/pdi/pack/{uprn}", async (
    string uprn,
    string? propertyType,
    int? internalAreaSqM,
    int? bedrooms,
    int? bathrooms,
    int? receptions,
    [FromKeyedServices("pdi")] IOpdaClient pdi,
    CancellationToken ct) =>
{
    var body = new
    {
        uprn,
        propertyType,
        internalAreaSqM,
        bedrooms,
        bathrooms,
        receptions,
    };
    var result = await pdi.PostAsync("/opda-opaque/appraisal/v1/property-pack/uprn", body, ct);
    return result is not null ? Results.Ok(result) : Results.StatusCode(502);
});

// ── GET /demo-api/pdi/state/{uprn} ───────────────────────────────────────────
// Materialised current-state view from PDI: address, valuation AVM, EPC, council
// tax, flood risk, planning history, sold prices, utilities.

app.MapGet("/demo-api/pdi/state/{uprn}", async (
    string uprn,
    [FromKeyedServices("pdi")] IOpdaClient pdi,
    CancellationToken ct) =>
{
    var result = await pdi.GetAsync($"/opda-opaque/current-state/{uprn}", ct);
    return result is not null ? Results.Ok(result) : Results.StatusCode(502);
});

// ── GET /demo-api/pdi/claims/{uprn} ──────────────────────────────────────────
// PDI-signed verified claims array (trust_framework: uk_pdtf).

app.MapGet("/demo-api/pdi/claims/{uprn}", async (
    string uprn,
    [FromKeyedServices("pdi")] IOpdaClient pdi,
    CancellationToken ct) =>
{
    var result = await pdi.GetAsync($"/opda-opaque/claims/{uprn}", ct);
    return result is not null ? Results.Ok(result) : Results.StatusCode(502);
});

// ── Sprift routes ─────────────────────────────────────────────────────────────
// Paths derived from the commercial Sprift API spec (basePath: /dashboard/api/v1).
// SPRIFT_BASE_URL default and SPRIFT_SCOPE need confirming from Alan / Sprift call
// on 2026-06-11 — update terraform/variables.tf defaults once confirmed.
// All three routes return 503 until SPRIFT_BASE_URL is set.

static IResult SpriftUnconfigured() =>
    Results.Problem(
        "Sprift PDTF endpoint not yet configured — set SPRIFT_BASE_URL and SPRIFT_SCOPE",
        statusCode: 503);

// ── GET /demo-api/sprift/material-information/{uprn} ─────────────────────────
// Parts A (financial/tenure), B (physical), C (environmental/planning).

app.MapGet("/demo-api/sprift/material-information/{uprn}", async (
    string uprn,
    HttpContext ctx,
    CancellationToken ct) =>
{
    if (!ctx.RequestServices.IsKeyedService(typeof(IOpdaClient), "sprift"))
        return SpriftUnconfigured();
    var sprift = ctx.RequestServices.GetRequiredKeyedService<IOpdaClient>("sprift");
    var result = await sprift.GetAsync($"/property/{uprn}/materialinformation", ct);
    return result is not null ? Results.Ok(result) : Results.StatusCode(502);
});

// ── GET /demo-api/sprift/property/{uprn} ─────────────────────────────────────
// Full property data: EPC, council tax, flood risk, AVM, land registry,
// interior, broadband, mobile coverage, polygon.

app.MapGet("/demo-api/sprift/property/{uprn}", async (
    string uprn,
    HttpContext ctx,
    CancellationToken ct) =>
{
    if (!ctx.RequestServices.IsKeyedService(typeof(IOpdaClient), "sprift"))
        return SpriftUnconfigured();
    var sprift = ctx.RequestServices.GetRequiredKeyedService<IOpdaClient>("sprift");
    var result = await sprift.GetAsync($"/property/{uprn}/search", ct);
    return result is not null ? Results.Ok(result) : Results.StatusCode(502);
});

// ── GET /demo-api/sprift/comparables/{uprn} ───────────────────────────────────
// Comparable properties near the target. Optional query params mirror the spec:
// status (available|sstc|forsalewithdrawn|soldwithdrawn — default: available),
// bedroom, type, price1, price2, searchDistance, sort, limit, days.

app.MapGet("/demo-api/sprift/comparables/{uprn}", async (
    string uprn,
    string? status,
    string? bedroom,
    string? type,
    int? price1,
    int? price2,
    double? searchDistance,
    int? sort,
    int? limit,
    int? days,
    HttpContext ctx,
    CancellationToken ct) =>
{
    if (!ctx.RequestServices.IsKeyedService(typeof(IOpdaClient), "sprift"))
        return SpriftUnconfigured();
    var sprift = ctx.RequestServices.GetRequiredKeyedService<IOpdaClient>("sprift");
    var listingStatus = status ?? "available";
    var qs = new List<string>();
    if (bedroom is not null)       qs.Add($"bedroom={bedroom}");
    if (type is not null)          qs.Add($"type={Uri.EscapeDataString(type)}");
    if (price1 is not null)        qs.Add($"price1={price1}");
    if (price2 is not null)        qs.Add($"price2={price2}");
    if (searchDistance is not null) qs.Add($"searchDistance={searchDistance}");
    if (sort is not null)          qs.Add($"sort={sort}");
    if (limit is not null)         qs.Add($"limit={limit}");
    if (days is not null)          qs.Add($"days={days}");
    var path = $"/property/{uprn}/{listingStatus}" + (qs.Count > 0 ? "?" + string.Join("&", qs) : "");
    var result = await sprift.GetAsync(path, ct);
    return result is not null ? Results.Ok(result) : Results.StatusCode(502);
});

// ── GET /demo-api/chain/{uprn} ────────────────────────────────────────────────
// Proxies ViewMyChain chain state. Returns the full chain response including
// properties, milestones, and chain type for display in the property chain card.

app.MapGet("/demo-api/chain/{uprn}", async (
    string uprn,
    [FromKeyedServices("vmc")] IOpdaClient vmc,
    CancellationToken ct) =>
{
    var result = await vmc.PostFormAsync("/api/v1/opda/chains",
    [
        new("inputType", "uprn"),
        new("value[]",   uprn),
    ], ct);
    return result is not null ? Results.Ok(result) : Results.StatusCode(502);
});

app.Run();

public partial class Program { }
