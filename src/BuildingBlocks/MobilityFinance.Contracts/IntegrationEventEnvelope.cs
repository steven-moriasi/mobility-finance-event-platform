using System.Text.Json;

namespace MobilityFinance.Contracts;

public sealed record IntegrationEventEnvelope(
    Guid MessageId,
    string EventType,
    int SchemaVersion,
    string AggregateId,
    long AggregateSequence,
    Guid CorrelationId,
    Guid? CausationId,
    DateTimeOffset OccurredAtUtc,
    string Market,
    string TenantId,
    string? TraceParent,
    JsonElement Payload);
