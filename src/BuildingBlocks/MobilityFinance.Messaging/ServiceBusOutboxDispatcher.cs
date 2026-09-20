using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MobilityFinance.Contracts;

namespace MobilityFinance.Messaging;

public sealed class ServiceBusOutboxDispatcher(
    InMemoryEventOutbox outbox,
    ServiceBusClient client,
    IOptions<ServiceBusOptions> options,
    ILogger<ServiceBusOutboxDispatcher> logger)
    : BackgroundService
{
    private static readonly Action<
        ILogger,
        Guid,
        int,
        Exception?> DeliveryFailed =
        LoggerMessage.Define<Guid, int>(
            LogLevel.Warning,
            new EventId(1, nameof(DeliveryFailed)),
            "Event {MessageId} delivery attempt {Attempt} failed");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using ServiceBusSender sender =
            client.CreateSender(options.Value.EventsTopic);

        while (!stoppingToken.IsCancellationRequested)
        {
            IReadOnlyList<OutboxMessage> messages = outbox.TakeDue(20);
            foreach (OutboxMessage message in messages)
            {
                await DispatchAsync(sender, message, stoppingToken);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), stoppingToken);
        }
    }

    private async Task DispatchAsync(
        ServiceBusSender sender,
        OutboxMessage outboxMessage,
        CancellationToken cancellationToken)
    {
        IntegrationEventEnvelope envelope = outboxMessage.Envelope;
        try
        {
            ServiceBusMessage message = new(
                BinaryData.FromString(JsonSerializer.Serialize(envelope)))
            {
                MessageId = envelope.MessageId.ToString(),
                CorrelationId = envelope.CorrelationId.ToString(),
                ContentType = "application/json",
                Subject = envelope.EventType,
                SessionId = envelope.AggregateId,
            };
            message.ApplicationProperties["schemaVersion"] =
                envelope.SchemaVersion;
            message.ApplicationProperties["tenantId"] = envelope.TenantId;
            message.ApplicationProperties["market"] = envelope.Market;

            await sender.SendMessageAsync(message, cancellationToken);
            outbox.MarkPublished(envelope.MessageId);
        }
        catch (Exception exception)
            when (exception is ServiceBusException
                or TimeoutException
                or OperationCanceledException)
        {
            if (exception is OperationCanceledException
                && cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            DeliveryFailed(
                logger,
                envelope.MessageId,
                outboxMessage.Attempts + 1,
                exception);
            outbox.Reschedule(envelope.MessageId, exception.Message);
        }
    }
}
