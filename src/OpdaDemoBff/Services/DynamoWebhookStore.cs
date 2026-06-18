using System.Text;
using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Options;
using OpdaDemoBff.Config;
using OpdaDemoBff.Models;

namespace OpdaDemoBff.Services;

public class DynamoWebhookStore(IAmazonDynamoDB dynamo, IOptions<DynamoConfig> config) : IWebhookStore
{
    private static readonly TimeSpan Retention = TimeSpan.FromDays(7);
    private string TableName => config.Value.TableName;

    public async Task StoreAsync(string rawBody, CancellationToken ct = default)
    {
        var (transactionDid, eventType) = DecodeJwtClaims(rawBody);
        var receivedAt = DateTimeOffset.UtcNow;

        await dynamo.PutItemAsync(new PutItemRequest
        {
            TableName = TableName,
            Item = new Dictionary<string, AttributeValue>
            {
                ["transactionDid"] = new AttributeValue { S = transactionDid },
                ["event"]          = new AttributeValue { S = eventType },
                ["rawBody"]        = new AttributeValue { S = rawBody },
                ["receivedAt"]     = new AttributeValue { S = receivedAt.ToString("O") },
                ["ttl"]            = new AttributeValue { N = ((long)(receivedAt + Retention).ToUnixTimeSeconds()).ToString() },
            }
        }, ct);
    }

    public async Task<IReadOnlyList<WebhookEvent>> ListAsync(string transactionDid, CancellationToken ct = default)
    {
        var response = await dynamo.QueryAsync(new QueryRequest
        {
            TableName                 = TableName,
            KeyConditionExpression    = "transactionDid = :did",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":did"] = new AttributeValue { S = transactionDid }
            }
        }, ct);

        return response.Items
            .Select(Map)
            .OrderBy(e => e.ReceivedAt)
            .ToList();
    }

    public async Task<WebhookEvent?> GetAsync(string transactionDid, string eventType, CancellationToken ct = default)
    {
        var response = await dynamo.GetItemAsync(new GetItemRequest
        {
            TableName = TableName,
            Key       = new Dictionary<string, AttributeValue>
            {
                ["transactionDid"] = new AttributeValue { S = transactionDid },
                ["event"]          = new AttributeValue { S = eventType },
            }
        }, ct);

        return response.Item.Count > 0 ? Map(response.Item) : null;
    }

    private static (string transactionDid, string eventType) DecodeJwtClaims(string rawJwt)
    {
        var parts = rawJwt.Split('.');
        if (parts.Length < 2)
            throw new ArgumentException("Not a valid JWT");

        var base64 = parts[1].Replace('-', '+').Replace('_', '/');
        var padded  = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
        var json    = Encoding.UTF8.GetString(Convert.FromBase64String(padded));

        using var doc  = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var transactionDid = root.GetProperty("data").GetProperty("transactionDid").GetString()
            ?? throw new ArgumentException("Missing data.transactionDid in JWT payload");
        var eventType = root.GetProperty("event").GetString()
            ?? throw new ArgumentException("Missing event in JWT payload");

        return (transactionDid, eventType);
    }

    private static WebhookEvent Map(Dictionary<string, AttributeValue> item) => new(
        TransactionDid: item["transactionDid"].S,
        Event:          item["event"].S,
        RawBody:        item["rawBody"].S,
        ReceivedAt:     item["receivedAt"].S,
        Ttl:            long.Parse(item["ttl"].N)
    );
}
