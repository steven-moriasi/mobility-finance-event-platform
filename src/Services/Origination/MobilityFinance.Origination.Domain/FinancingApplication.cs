namespace MobilityFinance.Origination.Domain;

public sealed class FinancingApplication
{
    private FinancingApplication(
        Guid id,
        string applicantReference,
        string market,
        Money assetPrice,
        Money deposit,
        int termWeeks,
        Money weeklyIncome,
        DateTimeOffset submittedAtUtc)
    {
        Id = id;
        ApplicantReference = applicantReference;
        Market = market;
        AssetPrice = assetPrice;
        Deposit = deposit;
        TermWeeks = termWeeks;
        WeeklyIncome = weeklyIncome;
        SubmittedAtUtc = submittedAtUtc;
        Status = ApplicationStatus.Submitted;
    }

    public Guid Id { get; }

    public string ApplicantReference { get; }

    public string Market { get; }

    public Money AssetPrice { get; }

    public Money Deposit { get; }

    public int TermWeeks { get; }

    public Money WeeklyIncome { get; }

    public DateTimeOffset SubmittedAtUtc { get; }

    public ApplicationStatus Status { get; private set; }

    public FinancingOffer? Offer { get; private set; }

    public FinancingAgreement? Agreement { get; private set; }

    public static FinancingApplication Submit(
        string applicantReference,
        string market,
        Money assetPrice,
        Money deposit,
        int termWeeks,
        Money weeklyIncome,
        DateTimeOffset submittedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(applicantReference))
        {
            throw new ArgumentException(
                "Applicant reference is required.",
                nameof(applicantReference));
        }

        if (!string.Equals(market, "KE", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "The current pricing policy supports the KE market.",
                nameof(market));
        }

        return new FinancingApplication(
            Guid.NewGuid(),
            applicantReference.Trim(),
            market.ToUpperInvariant(),
            assetPrice,
            deposit,
            termWeeks,
            weeklyIncome,
            submittedAtUtc);
    }

    public FinancingOffer Price(PricingPolicy policy, DateTimeOffset pricedAtUtc)
    {
        if (Status != ApplicationStatus.Submitted)
        {
            throw new InvalidOperationException("Only submitted applications can be priced.");
        }

        Offer = policy.Price(
            Id,
            AssetPrice,
            Deposit,
            TermWeeks,
            WeeklyIncome,
            pricedAtUtc);
        Status = ApplicationStatus.Priced;

        return Offer;
    }

    public FinancingAgreement AcceptOffer(
        Guid offerId,
        DateTimeOffset acceptedAtUtc)
    {
        if (Status != ApplicationStatus.Priced || Offer is null)
        {
            throw new InvalidOperationException("A priced offer is required.");
        }

        if (Offer.Id != offerId)
        {
            throw new InvalidOperationException("The offer does not belong to this application.");
        }

        if (acceptedAtUtc > Offer.ExpiresAtUtc)
        {
            throw new InvalidOperationException("The offer has expired.");
        }

        Agreement = new FinancingAgreement(
            Guid.NewGuid(),
            Id,
            Offer.Id,
            ApplicantReference,
            Offer.PolicyVersion,
            Offer.Currency,
            Offer.Deposit,
            Offer.TotalRepayable,
            Offer.Schedule,
            acceptedAtUtc);
        Status = ApplicationStatus.Accepted;

        return Agreement;
    }
}

public sealed record FinancingAgreement(
    Guid Id,
    Guid ApplicationId,
    Guid OfferId,
    string ApplicantReference,
    string PolicyVersion,
    string Currency,
    Money DepositRequired,
    Money TotalRepayable,
    IReadOnlyList<RepaymentInstallment> Schedule,
    DateTimeOffset CreatedAtUtc);
