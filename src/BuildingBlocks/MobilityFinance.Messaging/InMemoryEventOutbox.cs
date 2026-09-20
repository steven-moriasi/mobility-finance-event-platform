using MobilityFinance.Contracts;

namespace MobilityFinance.Messaging;

public sealed class InMemoryEventOutbox(TimeProvider timeProvider)
{
    private const int MaximumAttempts = 8;
    private readonly Lock _lock = new();
    private readonly List<OutboxMessage> _messages = [];

    public void Enqueue(IntegrationEventEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        lock (_lock)
        {
            _messages.Add(
                new OutboxMessage(
                    envelope,
                    Attempts: 0,
                    NextAttemptAtUtc: timeProvider.GetUtcNow(),
                    PublishedAtUtc: null,
                    LastError: null));
        }
    }

    public IReadOnlyList<OutboxMessage> TakeDue(int maximumCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCount);
        DateTimeOffset now = timeProvider.GetUtcNow();
        lock (_lock)
        {
            return _messages
                .Where(
                    message => message.PublishedAtUtc is null
                        && message.Attempts < MaximumAttempts
                        && message.NextAttemptAtUtc <= now)
                .OrderBy(message => message.NextAttemptAtUtc)
                .Take(maximumCount)
                .ToArray();
        }
    }

    public void MarkPublished(Guid messageId)
    {
        Update(
            messageId,
            message => message with
            {
                PublishedAtUtc = timeProvider.GetUtcNow(),
                LastError = null,
            });
    }

    public void Reschedule(Guid messageId, string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        Update(
            messageId,
            message =>
            {
                int attempts = message.Attempts + 1;
                double delaySeconds = Math.Min(
                    60,
                    Math.Pow(2, attempts));
                return message with
                {
                    Attempts = attempts,
                    NextAttemptAtUtc = timeProvider
                        .GetUtcNow()
                        .AddSeconds(delaySeconds),
                    LastError = error,
                };
            });
    }

    public OutboxSummary GetSummary()
    {
        lock (_lock)
        {
            return new OutboxSummary(
                _messages.Count(message => message.PublishedAtUtc is null),
                _messages.Count(message => message.PublishedAtUtc is not null),
                _messages.Count(
                    message => message.PublishedAtUtc is null
                        && message.Attempts >= MaximumAttempts));
        }
    }

    private void Update(
        Guid messageId,
        Func<OutboxMessage, OutboxMessage> update)
    {
        lock (_lock)
        {
            int index = _messages.FindIndex(
                message => message.Envelope.MessageId == messageId);
            if (index < 0)
            {
                throw new KeyNotFoundException("Outbox message was not found.");
            }

            _messages[index] = update(_messages[index]);
        }
    }
}

public sealed record OutboxMessage(
    IntegrationEventEnvelope Envelope,
    int Attempts,
    DateTimeOffset NextAttemptAtUtc,
    DateTimeOffset? PublishedAtUtc,
    string? LastError);

public sealed record OutboxSummary(int Pending, int Published, int Failed);
