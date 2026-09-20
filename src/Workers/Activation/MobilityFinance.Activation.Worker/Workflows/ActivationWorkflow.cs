namespace MobilityFinance.Activation.Worker.Workflows;

public sealed class ActivationWorkflow
{
    private readonly List<ActivationTimelineEntry> _timeline = [];

    private ActivationWorkflow(
        Guid agreementId,
        string customerReference,
        decimal depositRequired,
        string currency,
        DateTimeOffset startedAtUtc,
        long aggregateSequence)
    {
        AgreementId = agreementId;
        CustomerReference = customerReference;
        DepositRequired = depositRequired;
        Currency = currency;
        StartedAtUtc = startedAtUtc;
        LastAggregateSequence = aggregateSequence;
        State = ActivationWorkflowState.AwaitingDeposit;
        AddTimeline("AgreementCreated", startedAtUtc, "Activation workflow started.");
    }

    public Guid AgreementId { get; }

    public string CustomerReference { get; }

    public decimal DepositRequired { get; }

    public string Currency { get; }

    public ActivationWorkflowState State { get; private set; }

    public bool DepositRecorded { get; private set; }

    public Guid? AssetId { get; private set; }

    public string? AssetReference { get; private set; }

    public DateTimeOffset StartedAtUtc { get; }

    public DateTimeOffset? ActivatedAtUtc { get; private set; }

    public long LastAggregateSequence { get; private set; }

    public IReadOnlyList<ActivationTimelineEntry> Timeline => _timeline;

    public static ActivationWorkflow Start(
        Guid agreementId,
        string customerReference,
        decimal depositRequired,
        string currency,
        DateTimeOffset startedAtUtc,
        long aggregateSequence)
    {
        if (agreementId == Guid.Empty)
        {
            throw new ArgumentException(
                "Agreement ID is required.",
                nameof(agreementId));
        }

        if (string.IsNullOrWhiteSpace(customerReference))
        {
            throw new ArgumentException(
                "Customer reference is required.",
                nameof(customerReference));
        }

        if (depositRequired < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(depositRequired),
                "Required deposit cannot be negative.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        return new ActivationWorkflow(
            agreementId,
            customerReference.Trim(),
            depositRequired,
            currency.Trim().ToUpperInvariant(),
            startedAtUtc,
            aggregateSequence);
    }

    public bool RecordDeposit(
        Guid paymentId,
        DateTimeOffset recordedAtUtc,
        long aggregateSequence)
    {
        if (DepositRecorded)
        {
            AdvanceSequence(aggregateSequence);
            return false;
        }

        DepositRecorded = true;
        AdvanceSequence(aggregateSequence);
        AddTimeline(
            "DepositRecorded",
            recordedAtUtc,
            $"Deposit satisfied by payment {paymentId}.");
        return TryActivate(recordedAtUtc);
    }

    public bool AssignAsset(
        Guid assetId,
        string assetReference,
        DateTimeOffset assignedAtUtc,
        long aggregateSequence)
    {
        if (AssetId.HasValue)
        {
            if (AssetId != assetId)
            {
                throw new InvalidOperationException(
                    "The workflow already has a different asset assignment.");
            }

            AdvanceSequence(aggregateSequence);
            return false;
        }

        if (assetId == Guid.Empty)
        {
            throw new ArgumentException("Asset ID is required.", nameof(assetId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(assetReference);
        AssetId = assetId;
        AssetReference = assetReference.Trim();
        AdvanceSequence(aggregateSequence);
        AddTimeline(
            "AssetAssigned",
            assignedAtUtc,
            $"Asset {AssetReference} assigned.");
        return TryActivate(assignedAtUtc);
    }

    private bool TryActivate(DateTimeOffset occurredAtUtc)
    {
        if (State == ActivationWorkflowState.Activated)
        {
            return false;
        }

        if (DepositRecorded && AssetId.HasValue)
        {
            State = ActivationWorkflowState.Activated;
            ActivatedAtUtc = occurredAtUtc;
            AddTimeline(
                "AgreementActivated",
                occurredAtUtc,
                "Deposit and asset assignment requirements satisfied.");
            return true;
        }

        State = DepositRecorded
            ? ActivationWorkflowState.AwaitingAsset
            : ActivationWorkflowState.AwaitingDeposit;
        return false;
    }

    private void AdvanceSequence(long aggregateSequence)
    {
        LastAggregateSequence = Math.Max(
            LastAggregateSequence,
            aggregateSequence);
    }

    private void AddTimeline(
        string eventType,
        DateTimeOffset occurredAtUtc,
        string description)
    {
        _timeline.Add(
            new ActivationTimelineEntry(
                eventType,
                occurredAtUtc,
                description));
    }
}

public enum ActivationWorkflowState
{
    AwaitingDeposit,
    AwaitingAsset,
    Activated,
}

public sealed record ActivationTimelineEntry(
    string EventType,
    DateTimeOffset OccurredAtUtc,
    string Description);
