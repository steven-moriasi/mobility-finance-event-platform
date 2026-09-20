using System.Diagnostics;
using System.Text.Json;

namespace MobilityFinance.Contracts;

public static class IntegrationEventFactory
{
    public static IntegrationEventEnvelope Create<TPayload>(
        string eventType,
        string aggregateId,
        long aggregateSequence,
        Guid correlationId,
        Guid? causationId,
        DateTimeOffset occurredAtUtc,
        TPayload payload,
        string market = "KE",
        string tenantId = "synthetic-portfolio")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
        ArgumentException.ThrowIfNullOrWhiteSpace(market);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(aggregateSequence);

        return new IntegrationEventEnvelope(
            Guid.NewGuid(),
            eventType,
            SchemaVersion: 1,
            aggregateId,
            aggregateSequence,
            correlationId,
            causationId,
            occurredAtUtc,
            market.ToUpperInvariant(),
            tenantId,
            Activity.Current?.Id,
            JsonSerializer.SerializeToElement(payload));
    }
}
