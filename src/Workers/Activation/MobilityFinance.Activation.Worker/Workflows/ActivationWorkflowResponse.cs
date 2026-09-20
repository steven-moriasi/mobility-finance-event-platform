namespace MobilityFinance.Activation.Worker.Workflows;

public sealed record ActivationWorkflowResponse(
    Guid AgreementId,
    string CustomerReference,
    string State,
    bool DepositRecorded,
    Guid? AssetId,
    string? AssetReference,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? ActivatedAtUtc,
    IReadOnlyList<ActivationTimelineEntry> Timeline)
{
    public static ActivationWorkflowResponse From(
        ActivationWorkflow workflow) =>
        new(
            workflow.AgreementId,
            workflow.CustomerReference,
            workflow.State.ToString(),
            workflow.DepositRecorded,
            workflow.AssetId,
            workflow.AssetReference,
            workflow.StartedAtUtc,
            workflow.ActivatedAtUtc,
            workflow.Timeline);
}
