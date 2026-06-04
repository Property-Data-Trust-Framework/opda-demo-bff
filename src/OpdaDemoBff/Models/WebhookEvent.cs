namespace OpdaDemoBff.Models;

public record WebhookEvent(
    string EventId,
    string RawBody,
    string ReceivedAt,
    long Ttl
);
