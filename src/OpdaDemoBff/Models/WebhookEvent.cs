namespace OpdaDemoBff.Models;

public record WebhookEvent(
    string TransactionDid,
    string Event,
    string RawBody,
    string ReceivedAt,
    long   Ttl
);
