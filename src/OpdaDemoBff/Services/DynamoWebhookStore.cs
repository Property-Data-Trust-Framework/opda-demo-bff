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

    public async Task<string> StoreAsync(string rawBody, CancellationToken ct = default)
    {
        var eventId    = Guid.NewGuid().ToString();
        var receivedAt = DateTimeOffset.UtcNow;

        await dynamo.PutItemAsync(new PutItemRequest
        {
            TableName = TableName,
            Item = new Dictionary<string, AttributeValue>
            {
                ["eventId"]    = new AttributeValue { S = eventId },
                ["rawBody"]    = new AttributeValue { S = rawBody },
                ["receivedAt"] = new AttributeValue { S = receivedAt.ToString("O") },
                ["ttl"]        = new AttributeValue { N = ((long)(receivedAt + Retention).ToUnixTimeSeconds()).ToString() },
            }
        }, ct);

        return eventId;
    }

    public async Task<IReadOnlyList<WebhookEvent>> ListAsync(int limit = 50, CancellationToken ct = default)
    {
        var response = await dynamo.ScanAsync(new ScanRequest
        {
            TableName = TableName,
            Limit     = limit,
        }, ct);

        return response.Items
            .Select(Map)
            .OrderByDescending(e => e.ReceivedAt)
            .ToList();
    }

    public async Task<WebhookEvent?> GetAsync(string eventId, CancellationToken ct = default)
    {
        var response = await dynamo.GetItemAsync(new GetItemRequest
        {
            TableName = TableName,
            Key       = new Dictionary<string, AttributeValue>
            {
                ["eventId"] = new AttributeValue { S = eventId }
            }
        }, ct);

        return response.Item.Count > 0 ? Map(response.Item) : null;
    }

    private static WebhookEvent Map(Dictionary<string, AttributeValue> item) => new(
        EventId:    item["eventId"].S,
        RawBody:    item["rawBody"].S,
        ReceivedAt: item["receivedAt"].S,
        Ttl:        long.Parse(item["ttl"].N)
    );
}
