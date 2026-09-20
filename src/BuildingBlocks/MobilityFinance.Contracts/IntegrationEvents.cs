namespace MobilityFinance.Contracts;

public static class IntegrationEventTypes
{
    public const string AgreementCreated = nameof(AgreementCreated);
    public const string DepositRecorded = nameof(DepositRecorded);
    public const string AssetAssigned = nameof(AssetAssigned);
    public const string AgreementActivated = nameof(AgreementActivated);
    public const string PaymentRecorded = nameof(PaymentRecorded);
    public const string RepaymentAllocated = nameof(RepaymentAllocated);
    public const string AccountReviewRequired = nameof(AccountReviewRequired);
}

public sealed record AgreementCreatedEvent(
    Guid AgreementId,
    Guid ApplicationId,
    string CustomerReference,
    decimal DepositRequired,
    string Currency);

public sealed record DepositRecordedEvent(
    Guid AgreementId,
    Guid AccountId,
    Guid PaymentId,
    decimal Amount,
    string Currency);

public sealed record AssetAssignedEvent(
    Guid AgreementId,
    Guid AssetId,
    string AssetReference);

public sealed record AgreementActivatedEvent(
    Guid AgreementId,
    Guid AssetId,
    DateTimeOffset ActivatedAtUtc);

public sealed record PaymentRecordedEvent(
    Guid AgreementId,
    Guid AccountId,
    Guid PaymentId,
    string Provider,
    string ProviderTransactionId,
    decimal Amount,
    string Currency);

public sealed record RepaymentAllocatedEvent(
    Guid AgreementId,
    Guid AccountId,
    Guid PaymentId,
    decimal DepositApplied,
    decimal RepaymentApplied,
    decimal CustomerCredit,
    string Currency);

public sealed record AccountReviewRequiredEvent(
    Guid AgreementId,
    string Reason,
    DateTimeOffset RaisedAtUtc);
