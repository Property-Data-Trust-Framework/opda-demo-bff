using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.Extensions.Options;
using OpdaDemoBff.Config;
using OpdaDemoBff.Models;

namespace OpdaDemoBff.Services;

public class DynamoWalletPresentationStore(IAmazonDynamoDB dynamo, IOptions<WalletStoreConfig> config) : IWalletPresentationStore
{
    private static readonly TimeSpan Retention = TimeSpan.FromHours(1);
    private string TableName => config.Value.TableName;

    public async Task CreateAsync(
        string state, string transactionDid, IReadOnlyList<string> credentialTypes, string nonce, CancellationToken ct = default)
    {
        var createdAt = DateTimeOffset.UtcNow;

        await dynamo.PutItemAsync(new PutItemRequest
        {
            TableName = TableName,
            Item = new Dictionary<string, AttributeValue>
            {
                ["state"]           = new AttributeValue { S = state },
                ["transactionDid"]  = new AttributeValue { S = transactionDid },
                ["credentialTypes"] = new AttributeValue { S = JsonSerializer.Serialize(credentialTypes) },
                ["nonce"]           = new AttributeValue { S = nonce },
                ["status"]          = new AttributeValue { S = "pending" },
                ["createdAt"]       = new AttributeValue { S = createdAt.ToString("O") },
                ["ttl"]             = new AttributeValue { N = ((long)(createdAt + Retention).ToUnixTimeSeconds()).ToString() },
            }
        }, ct);
    }

    public async Task<WalletPresentation?> GetAsync(string state, CancellationToken ct = default)
    {
        var response = await dynamo.GetItemAsync(new GetItemRequest
        {
            TableName = TableName,
            Key = new Dictionary<string, AttributeValue> { ["state"] = new AttributeValue { S = state } },
        }, ct);

        return response.Item.Count > 0 ? Map(response.Item) : null;
    }

    public async Task CompleteAsync(string state, WalletVerificationOutcome outcome, CancellationToken ct = default)
    {
        var values = new Dictionary<string, AttributeValue>
        {
            [":status"]      = new AttributeValue { S = outcome.Verified ? "verified" : "failed" },
            [":verifiedAt"]  = new AttributeValue { S = DateTimeOffset.UtcNow.ToString("O") },
            [":credentials"] = new AttributeValue { S = JsonSerializer.Serialize(outcome.Credentials) },
        };
        var setExpr = "SET #status = :status, verifiedAt = :verifiedAt, credentials = :credentials";

        if (outcome.FailureReason is not null)
        {
            values[":failureReason"] = new AttributeValue { S = outcome.FailureReason };
            setExpr += ", failureReason = :failureReason";
        }

        await dynamo.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = TableName,
            Key = new Dictionary<string, AttributeValue> { ["state"] = new AttributeValue { S = state } },
            UpdateExpression = setExpr,
            // "status" is a DynamoDB reserved word — must be aliased.
            ExpressionAttributeNames = new Dictionary<string, string> { ["#status"] = "status" },
            ExpressionAttributeValues = values,
        }, ct);
    }

    private static WalletPresentation Map(Dictionary<string, AttributeValue> item) => new(
        State: item["state"].S,
        TransactionDid: item["transactionDid"].S,
        CredentialTypes: JsonSerializer.Deserialize<List<string>>(item["credentialTypes"].S) ?? [],
        Nonce: item["nonce"].S,
        Status: item["status"].S,
        CreatedAt: item["createdAt"].S,
        Ttl: long.Parse(item["ttl"].N),
        Credentials: item.TryGetValue("credentials", out var c)
            ? JsonSerializer.Deserialize<List<VerifiedCredential>>(c.S)
            : null,
        FailureReason: item.TryGetValue("failureReason", out var f) ? f.S : null,
        VerifiedAt: item.TryGetValue("verifiedAt", out var v) ? v.S : null
    );
}
