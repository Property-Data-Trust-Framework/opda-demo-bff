using Amazon.DynamoDBv2;
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

app.Run();

public partial class Program { }
