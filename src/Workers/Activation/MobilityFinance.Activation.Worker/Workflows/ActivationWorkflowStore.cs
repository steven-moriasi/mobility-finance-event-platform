using System.Text.Json;
using MobilityFinance.Contracts;

namespace MobilityFinance.Activation.Worker.Workflows;

public sealed class ActivationWorkflowStore
{
    private readonly Lock _lock = new();
    private readonly Dictionary<Guid, ActivationWorkflow> _workflows = [];
    private readonly HashSet<Guid> _inbox = [];

    public ActivationProcessingResult Apply(
        IntegrationEventEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        lock (_lock)
        {
            if (!_inbox.Add(envelope.MessageId))
            {
                return new ActivationProcessingResult(
                    IsDuplicate: true,
                    ActivatedWorkflow: null);
            }

            ActivationWorkflow? activated = envelope.EventType switch
            {
                IntegrationEventTypes.AgreementCreated =>
                    ApplyAgreementCreated(envelope),
                IntegrationEventTypes.DepositRecorded =>
                    ApplyDepositRecorded(envelope),
                IntegrationEventTypes.AssetAssigned =>
                    ApplyAssetAssigned(envelope),
                _ => null,
            };

            return new ActivationProcessingResult(
                IsDuplicate: false,
                ActivatedWorkflow: activated);
        }
    }

    public ActivationWorkflow? Get(Guid agreementId)
    {
        lock (_lock)
        {
            return _workflows.GetValueOrDefault(agreementId);
        }
    }

    public IReadOnlyList<ActivationWorkflow> List()
    {
        lock (_lock)
        {
            return _workflows.Values
                .OrderByDescending(workflow => workflow.StartedAtUtc)
                .ToArray();
        }
    }

    private ActivationWorkflow? ApplyAgreementCreated(
        IntegrationEventEnvelope envelope)
    {
        AgreementCreatedEvent payload =
            envelope.Payload.Deserialize<AgreementCreatedEvent>()
            ?? throw new InvalidOperationException(
                "AgreementCreated payload is required.");
        if (_workflows.ContainsKey(payload.AgreementId))
        {
            return null;
        }

        _workflows.Add(
            payload.AgreementId,
            ActivationWorkflow.Start(
                payload.AgreementId,
                payload.CustomerReference,
                payload.DepositRequired,
                payload.Currency,
                envelope.OccurredAtUtc,
                envelope.AggregateSequence));
        return null;
    }

    private ActivationWorkflow? ApplyDepositRecorded(
        IntegrationEventEnvelope envelope)
    {
        DepositRecordedEvent payload =
            envelope.Payload.Deserialize<DepositRecordedEvent>()
            ?? throw new InvalidOperationException(
                "DepositRecorded payload is required.");
        ActivationWorkflow workflow = GetRequired(payload.AgreementId);
        return workflow.RecordDeposit(
            payload.PaymentId,
            envelope.OccurredAtUtc,
            envelope.AggregateSequence)
            ? workflow
            : null;
    }

    private ActivationWorkflow? ApplyAssetAssigned(
        IntegrationEventEnvelope envelope)
    {
        AssetAssignedEvent payload =
            envelope.Payload.Deserialize<AssetAssignedEvent>()
            ?? throw new InvalidOperationException(
                "AssetAssigned payload is required.");
        ActivationWorkflow workflow = GetRequired(payload.AgreementId);
        return workflow.AssignAsset(
            payload.AssetId,
            payload.AssetReference,
            envelope.OccurredAtUtc,
            envelope.AggregateSequence)
            ? workflow
            : null;
    }

    private ActivationWorkflow GetRequired(Guid agreementId)
    {
        return _workflows.GetValueOrDefault(agreementId)
            ?? throw new InvalidOperationException(
                "Activation workflow must start with AgreementCreated.");
    }
}

public sealed record ActivationProcessingResult(
    bool IsDuplicate,
    ActivationWorkflow? ActivatedWorkflow);
