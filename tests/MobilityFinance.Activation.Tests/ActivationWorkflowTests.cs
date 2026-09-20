using MobilityFinance.Activation.Worker.Workflows;
using MobilityFinance.Contracts;

namespace MobilityFinance.Activation.Tests;

public sealed class ActivationWorkflowTests
{
    private static readonly DateTimeOffset StartedAt =
        new(2026, 9, 20, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DepositBeforeAssetMovesWorkflowToAwaitingAsset()
    {
        ActivationWorkflow workflow = StartWorkflow();

        bool activated = workflow.RecordDeposit(
            Guid.NewGuid(),
            StartedAt.AddMinutes(5),
            aggregateSequence: 2);

        Assert.False(activated);
        Assert.True(workflow.DepositRecorded);
        Assert.Equal(
            ActivationWorkflowState.AwaitingAsset,
            workflow.State);
    }

    [Fact]
    public void AssetAndDepositActivateAgreementExactlyOnce()
    {
        ActivationWorkflow workflow = StartWorkflow();
        Guid assetId = Guid.NewGuid();
        workflow.AssignAsset(
            assetId,
            "BIKE-1001",
            StartedAt.AddMinutes(2),
            aggregateSequence: 2);

        bool activated = workflow.RecordDeposit(
            Guid.NewGuid(),
            StartedAt.AddMinutes(5),
            aggregateSequence: 3);
        bool duplicateActivation = workflow.RecordDeposit(
            Guid.NewGuid(),
            StartedAt.AddMinutes(6),
            aggregateSequence: 4);

        Assert.True(activated);
        Assert.False(duplicateActivation);
        Assert.Equal(ActivationWorkflowState.Activated, workflow.State);
        Assert.Equal(assetId, workflow.AssetId);
        Assert.Equal(4, workflow.Timeline.Count);
    }

    [Fact]
    public void InboxDeduplicatesRepeatedMessageId()
    {
        ActivationWorkflowStore store = new();
        IntegrationEventEnvelope envelope = AgreementCreatedEnvelope();

        ActivationProcessingResult first = store.Apply(envelope);
        ActivationProcessingResult duplicate = store.Apply(envelope);

        Assert.False(first.IsDuplicate);
        Assert.True(duplicate.IsDuplicate);
        Assert.Single(store.List());
    }

    private static ActivationWorkflow StartWorkflow()
    {
        return ActivationWorkflow.Start(
            Guid.NewGuid(),
            "SYNTH-5001",
            40_000m,
            "KES",
            StartedAt,
            aggregateSequence: 1);
    }

    private static IntegrationEventEnvelope AgreementCreatedEnvelope()
    {
        Guid agreementId = Guid.NewGuid();
        return IntegrationEventFactory.Create(
            IntegrationEventTypes.AgreementCreated,
            agreementId.ToString(),
            aggregateSequence: 1,
            agreementId,
            causationId: null,
            StartedAt,
            new AgreementCreatedEvent(
                agreementId,
                Guid.NewGuid(),
                "SYNTH-5001",
                40_000m,
                "KES"));
    }
}
