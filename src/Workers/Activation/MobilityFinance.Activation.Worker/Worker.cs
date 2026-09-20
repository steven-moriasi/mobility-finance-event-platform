using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using MobilityFinance.Activation.Worker.Workflows;
using MobilityFinance.Contracts;
using MobilityFinance.Messaging;

namespace MobilityFinance.Activation.Worker;

public sealed class Worker(
    ServiceBusClient client,
    IOptions<ServiceBusOptions> options,
    IConfiguration configuration,
    ActivationWorkflowStore workflows,
    InMemoryEventOutbox outbox)
    : BackgroundService
{
    private readonly string _subscription =
        configuration["ServiceBus:Subscription"]
        ?? throw new InvalidOperationException(
            "The activation Service Bus subscription is required.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using ServiceBusSessionProcessor processor =
            client.CreateSessionProcessor(
                options.Value.EventsTopic,
                _subscription,
                new ServiceBusSessionProcessorOptions
                {
                    AutoCompleteMessages = false,
                    MaxConcurrentSessions = 4,
                    MaxConcurrentCallsPerSession = 1,
                    MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(5),
                    SessionIdleTimeout = TimeSpan.FromSeconds(5),
                });
        processor.ProcessMessageAsync += ProcessMessageAsync;
        processor.ProcessErrorAsync += _ => Task.CompletedTask;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await processor.StartProcessingAsync(stoppingToken);
                await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
            }
            catch (ServiceBusException)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }

    private async Task ProcessMessageAsync(
        ProcessSessionMessageEventArgs args)
    {
        IntegrationEventEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<IntegrationEventEnvelope>(
                args.Message.Body);
        }
        catch (JsonException exception)
        {
            await args.DeadLetterMessageAsync(
                args.Message,
                "InvalidEnvelope",
                exception.Message);
            return;
        }

        if (envelope is null)
        {
            await args.DeadLetterMessageAsync(
                args.Message,
                "InvalidEnvelope",
                "Message body was empty.");
            return;
        }

        ActivationProcessingResult result = workflows.Apply(envelope);
        if (result.ActivatedWorkflow is not null)
        {
            ActivationWorkflow activated = result.ActivatedWorkflow;
            outbox.Enqueue(
                IntegrationEventFactory.Create(
                    IntegrationEventTypes.AgreementActivated,
                    activated.AgreementId.ToString(),
                    activated.LastAggregateSequence + 1,
                    envelope.CorrelationId,
                    envelope.MessageId,
                    activated.ActivatedAtUtc!.Value,
                    new AgreementActivatedEvent(
                        activated.AgreementId,
                        activated.AssetId!.Value,
                        activated.ActivatedAtUtc.Value)));
        }

        await args.CompleteMessageAsync(args.Message);
    }
}
