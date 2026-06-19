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

builder.Services.AddSingleton<IOpdaClient>(sp =>
    OpdaClient.CreateAsync(new OpdaClientConfig
    {
        ApiBaseUrl     = Environment.GetEnvironmentVariable("OPDA_API_BASE_URL") ?? "",
        ClientCertPath = Environment.GetEnvironmentVariable("OPDA_CLIENT_CERT_PATH") ?? "",
        ClientKeyPath  = Environment.GetEnvironmentVariable("OPDA_CLIENT_KEY_PATH") ?? "",
        SigningKeyPath  = Environment.GetEnvironmentVariable("OPDA_SIGNING_KEY_PATH") ?? "",
        ClientId        = Environment.GetEnvironmentVariable("OPDA_CLIENT_ID") ?? "",
        TokenEndpoint   = Environment.GetEnvironmentVariable("OPDA_TOKEN_ENDPOINT") ?? "",
        Scope           = Environment.GetEnvironmentVariable("OPDA_SCOPE") ?? "land-registry",
    }, sp.GetRequiredService<ILogger<OpdaClient>>()).GetAwaiter().GetResult());

// ViewMyChain — same mTLS cert + signing key, different base URL and scope.
builder.Services.AddKeyedSingleton<IOpdaClient>("vmc", (sp, _) =>
    OpdaClient.CreateAsync(new OpdaClientConfig
    {
        ApiBaseUrl     = Environment.GetEnvironmentVariable("VMC_BASE_URL") ?? "",
        ClientCertPath = Environment.GetEnvironmentVariable("OPDA_CLIENT_CERT_PATH") ?? "",
        ClientKeyPath  = Environment.GetEnvironmentVariable("OPDA_CLIENT_KEY_PATH") ?? "",
        SigningKeyPath  = Environment.GetEnvironmentVariable("OPDA_SIGNING_KEY_PATH") ?? "",
        ClientId        = Environment.GetEnvironmentVariable("OPDA_CLIENT_ID") ?? "",
        TokenEndpoint   = Environment.GetEnvironmentVariable("OPDA_TOKEN_ENDPOINT") ?? "",
        Scope           = Environment.GetEnvironmentVariable("VMC_SCOPE") ?? "transaction-status",
    }, sp.GetRequiredService<ILogger<OpdaClient>>()).GetAwaiter().GetResult());

// Property Deals Insight — same mTLS cert + signing key, scope: property-pack.
builder.Services.AddKeyedSingleton<IOpdaClient>("pdi", (sp, _) =>
    OpdaClient.CreateAsync(new OpdaClientConfig
    {
        ApiBaseUrl     = Environment.GetEnvironmentVariable("PDI_BASE_URL") ?? "",
        ClientCertPath = Environment.GetEnvironmentVariable("OPDA_CLIENT_CERT_PATH") ?? "",
        ClientKeyPath  = Environment.GetEnvironmentVariable("OPDA_CLIENT_KEY_PATH") ?? "",
        SigningKeyPath  = Environment.GetEnvironmentVariable("OPDA_SIGNING_KEY_PATH") ?? "",
        ClientId        = Environment.GetEnvironmentVariable("OPDA_CLIENT_ID") ?? "",
        TokenEndpoint   = Environment.GetEnvironmentVariable("OPDA_TOKEN_ENDPOINT") ?? "",
        Scope           = Environment.GetEnvironmentVariable("PDI_SCOPE") ?? "property-pack",
    }, sp.GetRequiredService<ILogger<OpdaClient>>()).GetAwaiter().GetResult());

// Sprift — mTLS + private_key_jwt (PDTF directory, scope: test) + x-api-key header (sandbox only).
// Skipped if SPRIFT_BASE_URL is not configured.
var spriftBaseUrl = Environment.GetEnvironmentVariable("SPRIFT_BASE_URL") ?? "";
if (!string.IsNullOrEmpty(spriftBaseUrl))
{
    builder.Services.AddKeyedSingleton<IOpdaClient>("sprift", (sp, _) =>
        OpdaClient.CreateAsync(new OpdaClientConfig
        {
            ApiBaseUrl      = spriftBaseUrl,
            ClientCertPath  = Environment.GetEnvironmentVariable("OPDA_CLIENT_CERT_PATH") ?? "",
            ClientKeyPath   = Environment.GetEnvironmentVariable("OPDA_CLIENT_KEY_PATH") ?? "",
            SigningKeyPath   = Environment.GetEnvironmentVariable("OPDA_SIGNING_KEY_PATH") ?? "",
            ClientId         = Environment.GetEnvironmentVariable("OPDA_CLIENT_ID") ?? "",
            TokenEndpoint    = Environment.GetEnvironmentVariable("OPDA_TOKEN_ENDPOINT") ?? "",
            Scope            = Environment.GetEnvironmentVariable("SPRIFT_SCOPE") ?? "property-pack",
            ApiKeyPath       = Environment.GetEnvironmentVariable("SPRIFT_API_KEY_PATH"),
            ApiKeyHeaderName = "x-api-key",
        }, sp.GetRequiredService<ILogger<OpdaClient>>()).GetAwaiter().GetResult());
}

// Smoove — API key only, no mTLS. Skipped if SMOOVE_BASE_URL is not set.
var smooveBaseUrl = Environment.GetEnvironmentVariable("SMOOVE_BASE_URL") ?? "";
if (!string.IsNullOrEmpty(smooveBaseUrl))
{
    builder.Services.AddSingleton<ISmooveClient>(sp =>
        SmooveClient.CreateAsync(
            smooveBaseUrl,
            Environment.GetEnvironmentVariable("SMOOVE_API_KEY_PATH") ?? "",
            sp.GetRequiredService<ILogger<SmooveClient>>()).GetAwaiter().GetResult());
}

var app = builder.Build();

// ── POST /webhook ─────────────────────────────────────────────────────────────
// Smoove delivers a signed RS256 JWT as the raw request body.
// We store it as-is for now to inspect the shape before adding JWT validation.

app.MapPost("/webhook", async (HttpRequest request, IWebhookStore store) =>
{
    using var reader = new StreamReader(request.Body);
    var rawBody = (await reader.ReadToEndAsync()).Trim();

    // Smoove delivers the JWT wrapped as a JSON string literal: "eyJ..."
    // Unwrap the outer quotes so we store the bare JWT.
    if (rawBody.StartsWith('"') && rawBody.EndsWith('"'))
        rawBody = rawBody[1..^1];

    if (string.IsNullOrWhiteSpace(rawBody))
        return Results.BadRequest(new { error = "Empty body" });

    await store.StoreAsync(rawBody);
    return Results.Ok(new { status = "stored" });
});

// ── GET /demo-api/events/{transactionDid} ────────────────────────────────────

app.MapGet("/demo-api/events/{transactionDid}", async (string transactionDid, IWebhookStore store) =>
    Results.Ok(await store.ListAsync(transactionDid)));

// ── GET /demo-api/events/{transactionDid}/{event} ────────────────────────────

app.MapGet("/demo-api/events/{transactionDid}/{event}", async (string transactionDid, string @event, IWebhookStore store) =>
{
    var evt = await store.GetAsync(transactionDid, @event);
    return evt is not null ? Results.Ok(evt) : Results.NotFound();
});

// ── GET /demo-api/health ──────────────────────────────────────────────────────

app.MapGet("/demo-api/health", () => Results.Ok(new { status = "healthy" }));

// ── GET /demo-api/address?q= ──────────────────────────────────────────────────
// Proxies OS Places. SPA uses this for the address search box.

app.MapGet("/demo-api/address", async (string q, IOpdaClient opda) =>
{
    var result = await opda.GetAsync($"/v1/places/find?query={Uri.EscapeDataString(q)}&maxresults=100");
    return result is not null ? Results.Ok(result) : Results.StatusCode(502);
});

// ── GET /demo-api/uprn/{uprn} ─────────────────────────────────────────────────
// Proxies UPRN Validator. Auto-fires after the agent invites the seller.

app.MapGet("/demo-api/uprn/{uprn}", async (string uprn, IOpdaClient opda) =>
{
    var result = await opda.GetAsync($"/v1/uprn/validate/{PadUprn(uprn)}");
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
    var paddedUprn = PadUprn(uprn);
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

    var epcTask = opda.GetAsync($"/v1/epc/{paddedUprn}", ct);
    var ctTask  = opda.GetAsync($"/v1/council-tax/{paddedUprn}", ct);
    var mraTask = opda.GetAsync($"/v1/coalfield/{paddedUprn}", ct);
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
    var result = await opda.GetAsync($"/v1/documents/{PadUprn(uprn)}");
    return result is not null ? Results.Ok(result) : Results.StatusCode(502);
});

// ── POST /demo-api/source-of-funds ────────────────────────────────────────────
// Proxies Armalytix. BFF generates a clientRequestId on behalf of the SPA.

app.MapPost("/demo-api/source-of-funds", async (IOpdaClient opda) =>
{
    var clientRequestId = Environment.GetEnvironmentVariable("ARMALYTIX_CLIENT_REQUEST_ID")
        ?? Guid.NewGuid().ToString();
    var result = await opda.GetAsync($"/v1/source-of-funds/{clientRequestId}");
    return result is not null ? Results.Ok(result) : Results.StatusCode(502);
});

// ── GET /demo-api/property-pack/{uprn} ───────────────────────────────────────
// Unified property pack: routes to PDI for known sample UPRNs, Sprift for all others.
// Response: { source: "sprift"|"pdi", data: <upstream response>, jwsSignature? }

var pdiSampleUprns = new HashSet<string>
{
    "100070482318","100022539219","100071300442","10093560622", "100022521703",
    "217048506",   "100022598809","10090437590", "217030257",  "202065685",
    "100022725034","100022750316","217075263",   "34095084",   "100070405179",
    "217018783",   "10091059238", "100022793956","217070918",  "100071428730",
    "217067551",   "200015088",   "100022599518","100070446046","100071271440",
    "217071178",   "100070385755","217016110",   "100022802693","100022780345",
    "10033622251", "34019425",    "5009691",     "100070561271","100022778424",
    "12003281",    "100022528887","202064990",   "202219254",  "5114578",
    "34171489",    "100022558342","100070565556","100022562980","100022530012",
    "100022540400","200082387",   "217028345",   "217126195",  "5167116",
};

app.MapGet("/demo-api/property-pack/{uprn}", async (
    string uprn,
    [FromKeyedServices("pdi")] IOpdaClient pdi,
    HttpContext ctx,
    CancellationToken ct) =>
{
    if (!pdiSampleUprns.Contains(uprn))
    {
        var sprift = ctx.RequestServices.GetKeyedService<IOpdaClient>("sprift");
        if (sprift is not null)
        {
            var spriftResult = await sprift.GetAsync($"/metainformation/v1.0.0/uprn/{uprn}", ct);
            if (spriftResult is not null)
                return Results.Ok(new { source = "sprift", data = spriftResult });
        }
    }

    var (pdiBody, pdiJws) = await pdi.PostWithJwsAsync(
        "/opda-opaque/appraisal/v1/property-pack/uprn", new { uprn }, ct);
    if (pdiBody is not null)
        return Results.Ok(new { source = "pdi", data = pdiBody, jwsSignature = pdiJws });

    return Results.StatusCode(502);
});

// ── GET /demo-api/pdi/pack/{uprn} ────────────────────────────────────────────
// Full PDTF v3.5.0 property pack from Property Deals Insight.
// Response: { data: <pack>, jwsSignature: <x-jws-signature header> }

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
    var body = new { uprn, propertyType, internalAreaSqM, bedrooms, bathrooms, receptions };
    var (data, jws) = await pdi.PostWithJwsAsync("/opda-opaque/appraisal/v1/property-pack/uprn", body, ct);
    return data is not null ? Results.Ok(new { data, jwsSignature = jws }) : Results.StatusCode(502);
});

// ── GET /demo-api/pdi/state/{uprn} ───────────────────────────────────────────
// Materialised current-state view from PDI.
// Response: { data: <state>, jwsSignature: <x-jws-signature header> }

app.MapGet("/demo-api/pdi/state/{uprn}", async (
    string uprn,
    [FromKeyedServices("pdi")] IOpdaClient pdi,
    CancellationToken ct) =>
{
    var (data, jws) = await pdi.GetWithJwsAsync($"/opda-opaque/current-state/{uprn}", ct);
    return data is not null ? Results.Ok(new { data, jwsSignature = jws }) : Results.StatusCode(502);
});

// ── GET /demo-api/pdi/claims/{uprn} ──────────────────────────────────────────
// PDI-signed verified claims array (trust_framework: uk_pdtf).
// Response: { data: <claims[]>, jwsSignature: <x-jws-signature header> }

app.MapGet("/demo-api/pdi/claims/{uprn}", async (
    string uprn,
    [FromKeyedServices("pdi")] IOpdaClient pdi,
    CancellationToken ct) =>
{
    var (data, jws) = await pdi.GetWithJwsAsync($"/opda-opaque/claims/{uprn}", ct);
    return data is not null ? Results.Ok(new { data, jwsSignature = jws }) : Results.StatusCode(502);
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
    var sprift = ctx.RequestServices.GetKeyedService<IOpdaClient>("sprift");
    if (sprift is null) return SpriftUnconfigured();
    var result = await sprift.GetAsync($"/metainformation/v1.0.0/uprn/{uprn}", ct);
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
    var sprift = ctx.RequestServices.GetKeyedService<IOpdaClient>("sprift");
    if (sprift is null) return SpriftUnconfigured();
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
    var sprift = ctx.RequestServices.GetKeyedService<IOpdaClient>("sprift");
    if (sprift is null) return SpriftUnconfigured();
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

// ── POST /demo-api/conveyancing/completion-set ────────────────────────────────
// Triggers Smoove to simulate the completion-date-set event. Smoove fires a
// signed JWT webhook back to POST /webhook, which stores it in DynamoDB.
// The SPA polls GET /demo-api/events every 15s and advances the flow on receipt.

app.MapPost("/demo-api/conveyancing/completion-set", async (ConveyRequest req, HttpContext ctx, CancellationToken ct) =>
{
    var smoove = ctx.RequestServices.GetService<ISmooveClient>();
    if (smoove is null) return Results.Problem("Smoove not configured — set SMOOVE_BASE_URL", statusCode: 503);
    var ok = await smoove.PostAsync("/internal/simulate/completion-set", new
    {
        transactionDid = req.TransactionDid,
        completionDate = DateTimeOffset.UtcNow.AddDays(14).ToString("yyyy-MM-ddTHH:mm:ssZ"),
    }, ct);
    return ok ? Results.Ok(new { status = "triggered" }) : Results.StatusCode(502);
});

// ── POST /demo-api/conveyancing/completion-actioned ───────────────────────────
// Also auto-triggers the TID simulate call so the TID webhook arrives
// automatically — mirroring how HMLR issues the TID after completion.

app.MapPost("/demo-api/conveyancing/completion-actioned", async (ConveyRequest req, HttpContext ctx, CancellationToken ct) =>
{
    var smoove = ctx.RequestServices.GetService<ISmooveClient>();
    if (smoove is null) return Results.Problem("Smoove not configured — set SMOOVE_BASE_URL", statusCode: 503);
    var ok = await smoove.PostAsync("/internal/simulate/completion-actioned", new
    {
        transactionDid = req.TransactionDid,
        completionDate = DateTimeOffset.UtcNow.AddDays(14).ToString("yyyy-MM-ddTHH:mm:ssZ"),
    }, ct);
    if (!ok) return Results.StatusCode(502);
    await smoove.PostAsync("/internal/simulate/tid-received", new
    {
        transactionDid = req.TransactionDid,
        tid = new
        {
            titleNumber      = "EXC10010",
            registrationDate = DateTimeOffset.UtcNow.AddDays(14).ToString("yyyy-MM-dd"),
            proprietors      = new[] { "Mr Robert Malytix" },
            tenure           = "freehold",
            propertyAddress  = "52 Festive Road, London",
            priceStated      = "£150,000",
            classOfTitle     = "absolute",
        },
    }, ct);
    return Results.Ok(new { status = "triggered" });
});

app.Run();

// UPRNs from OS Places are raw integers (e.g. 5114578); backing OPDA APIs require
// exactly 12 digits, zero-padded (e.g. 000005114578).
static string PadUprn(string uprn) => uprn.PadLeft(12, '0');

public partial class Program { }

record ConveyRequest(string TransactionDid);
