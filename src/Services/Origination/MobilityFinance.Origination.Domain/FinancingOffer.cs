namespace MobilityFinance.Origination.Domain;

public sealed record FinancingOffer(
    Guid Id,
    Guid ApplicationId,
    string PolicyVersion,
    string Currency,
    Money AssetPrice,
    Money Deposit,
    Money Principal,
    Money FinanceCharge,
    Money ServiceFee,
    Money TotalRepayable,
    int TermWeeks,
    DateTimeOffset PricedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    IReadOnlyList<RepaymentInstallment> Schedule);

public sealed record RepaymentInstallment(
    int Sequence,
    DateTimeOffset DueAtUtc,
    Money Amount);
