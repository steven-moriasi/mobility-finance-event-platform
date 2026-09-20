namespace MobilityFinance.Origination.Domain;

public sealed record PricingPolicy(
    string Version,
    string Currency,
    decimal MinimumDepositRate,
    decimal WeeklyFinanceRate,
    decimal ServiceFeeRate,
    decimal MaximumPrincipalToWeeklyIncome,
    int OfferValidityDays)
{
    public static PricingPolicy KenyaV1 { get; } = new(
        Version: "KE-2026.1",
        Currency: "KES",
        MinimumDepositRate: 0.10m,
        WeeklyFinanceRate: 0.0025m,
        ServiceFeeRate: 0.04m,
        MaximumPrincipalToWeeklyIncome: 40m,
        OfferValidityDays: 7);

    public FinancingOffer Price(
        Guid applicationId,
        Money assetPrice,
        Money deposit,
        int termWeeks,
        Money weeklyIncome,
        DateTimeOffset pricedAtUtc)
    {
        ValidateConfiguration();
        EnsureCurrency(assetPrice);
        EnsureCurrency(deposit);
        EnsureCurrency(weeklyIncome);

        if (assetPrice.Amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(assetPrice),
                "Asset price must be positive.");
        }

        if (deposit.Amount < assetPrice.Amount * MinimumDepositRate)
        {
            throw new PricingDeclinedException(
                $"Deposit must be at least {MinimumDepositRate:P0} of the asset price.");
        }

        if (deposit.Amount >= assetPrice.Amount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(deposit),
                "Deposit must be less than the asset price.");
        }

        if (termWeeks is < 12 or > 104)
        {
            throw new ArgumentOutOfRangeException(
                nameof(termWeeks),
                "Term must be between 12 and 104 weeks.");
        }

        if (weeklyIncome.Amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(weeklyIncome),
                "Weekly income must be positive.");
        }

        Money principal = assetPrice - deposit;
        if (principal.Amount > weeklyIncome.Amount * MaximumPrincipalToWeeklyIncome)
        {
            throw new PricingDeclinedException(
                "Requested principal exceeds the configured affordability boundary.");
        }

        Money financeCharge = new(
            principal.Amount * WeeklyFinanceRate * termWeeks,
            Currency);
        Money serviceFee = new(assetPrice.Amount * ServiceFeeRate, Currency);
        Money totalRepayable = principal + financeCharge + serviceFee;
        IReadOnlyList<RepaymentInstallment> schedule = BuildSchedule(
            totalRepayable,
            termWeeks,
            pricedAtUtc);

        return new FinancingOffer(
            Id: Guid.NewGuid(),
            ApplicationId: applicationId,
            PolicyVersion: Version,
            Currency: Currency,
            AssetPrice: assetPrice,
            Deposit: deposit,
            Principal: principal,
            FinanceCharge: financeCharge,
            ServiceFee: serviceFee,
            TotalRepayable: totalRepayable,
            TermWeeks: termWeeks,
            PricedAtUtc: pricedAtUtc,
            ExpiresAtUtc: pricedAtUtc.AddDays(OfferValidityDays),
            Schedule: schedule);
    }

    private static List<RepaymentInstallment> BuildSchedule(
        Money totalRepayable,
        int termWeeks,
        DateTimeOffset pricedAtUtc)
    {
        Money standardInstallment = new(
            totalRepayable.Amount / termWeeks,
            totalRepayable.Currency);
        List<RepaymentInstallment> installments = new(termWeeks);
        decimal allocated = 0;

        for (int sequence = 1; sequence <= termWeeks; sequence++)
        {
            Money amount = sequence == termWeeks
                ? new Money(totalRepayable.Amount - allocated, totalRepayable.Currency)
                : standardInstallment;
            allocated += amount.Amount;
            installments.Add(
                new RepaymentInstallment(
                    sequence,
                    pricedAtUtc.AddDays(sequence * 7),
                    amount));
        }

        return installments;
    }

    private void EnsureCurrency(Money value)
    {
        if (!string.Equals(value.Currency, Currency, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Pricing policy {Version} requires {Currency}.",
                nameof(value));
        }
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(Version)
            || string.IsNullOrWhiteSpace(Currency)
            || MinimumDepositRate is <= 0 or >= 1
            || WeeklyFinanceRate < 0
            || ServiceFeeRate < 0
            || MaximumPrincipalToWeeklyIncome <= 0
            || OfferValidityDays <= 0)
        {
            throw new InvalidOperationException("Pricing policy configuration is invalid.");
        }
    }
}

public sealed class PricingDeclinedException(string message) : InvalidOperationException(message);
