using MobilityFinance.Contracts;
using MobilityFinance.Messaging;

namespace MobilityFinance.Messaging.Tests;

public sealed class InMemoryEventOutboxTests
{
    private static readonly DateTimeOffset CurrentTime =
        new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void EnqueuedMessageIsImmediatelyAvailable()
    {
        InMemoryEventOutbox outbox = new(
            new FixedTimeProvider(CurrentTime));
        IntegrationEventEnvelope envelope = CreateEnvelope();

        outbox.Enqueue(envelope);

        OutboxMessage message = Assert.Single(outbox.TakeDue(10));
        Assert.Equal(envelope.MessageId, message.Envelope.MessageId);
        Assert.Equal(new OutboxSummary(1, 0, 0), outbox.GetSummary());
    }

    [Fact]
    public void PublishedMessageIsNotDispatchedAgain()
    {
        InMemoryEventOutbox outbox = new(
            new FixedTimeProvider(CurrentTime));
        IntegrationEventEnvelope envelope = CreateEnvelope();
        outbox.Enqueue(envelope);

        outbox.MarkPublished(envelope.MessageId);

        Assert.Empty(outbox.TakeDue(10));
        Assert.Equal(new OutboxSummary(0, 1, 0), outbox.GetSummary());
    }

    [Fact]
    public void FailedMessageUsesBoundedExponentialDelay()
    {
        AdjustableTimeProvider timeProvider = new(CurrentTime);
        InMemoryEventOutbox outbox = new(timeProvider);
        IntegrationEventEnvelope envelope = CreateEnvelope();
        outbox.Enqueue(envelope);

        outbox.Reschedule(envelope.MessageId, "service unavailable");

        Assert.Empty(outbox.TakeDue(10));
        timeProvider.Advance(TimeSpan.FromSeconds(2));
        OutboxMessage message = Assert.Single(outbox.TakeDue(10));
        Assert.Equal(1, message.Attempts);
        Assert.Equal("service unavailable", message.LastError);
    }

    private static IntegrationEventEnvelope CreateEnvelope()
    {
        return IntegrationEventFactory.Create(
            IntegrationEventTypes.AgreementCreated,
            Guid.NewGuid().ToString(),
            aggregateSequence: 1,
            Guid.NewGuid(),
            causationId: null,
            CurrentTime,
            new AgreementCreatedEvent(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "SYNTH-4001",
                30_000m,
                "KES"));
    }

    private sealed class FixedTimeProvider(DateTimeOffset current)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => current;
    }

    private sealed class AdjustableTimeProvider(DateTimeOffset current)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => current;

        public void Advance(TimeSpan duration)
        {
            current += duration;
        }
    }
}
