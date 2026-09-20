using MobilityFinance.Origination.Domain;

namespace MobilityFinance.Origination.Tests;

public sealed class PricingPolicyTests
{
    private static readonly DateTimeOffset PricedAt =
        new(2026, 1, 15, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PricingProducesAnExactRepaymentSchedule()
    {
        FinancingOffer offer = PricingPolicy.KenyaV1.Price(
            Guid.NewGuid(),
            new Money(250_000m, "KES"),
            new Money(30_000m, "KES"),
            52,
            new Money(12_000m, "KES"),
            PricedAt);

        Assert.Equal(52, offer.Schedule.Count);
        Assert.Equal(
            offer.TotalRepayable.Amount,
            offer.Schedule.Sum(installment => installment.Amount.Amount));
        Assert.Equal("KE-2026.1", offer.PolicyVersion);
        Assert.Equal(PricedAt.AddDays(7), offer.Schedule[0].DueAtUtc);
    }

    [Fact]
    public void DepositBelowThePolicyMinimumIsDeclined()
    {
        PricingDeclinedException exception = Assert.Throws<PricingDeclinedException>(
            () => PricingPolicy.KenyaV1.Price(
                Guid.NewGuid(),
                new Money(250_000m, "KES"),
                new Money(10_000m, "KES"),
                52,
                new Money(12_000m, "KES"),
                PricedAt));

        Assert.Contains("at least", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PrincipalOutsideTheAffordabilityBoundaryIsDeclined()
    {
        PricingDeclinedException exception = Assert.Throws<PricingDeclinedException>(
            () => PricingPolicy.KenyaV1.Price(
                Guid.NewGuid(),
                new Money(500_000m, "KES"),
                new Money(50_000m, "KES"),
                52,
                new Money(5_000m, "KES"),
                PricedAt));

        Assert.Contains("affordability", exception.Message, StringComparison.Ordinal);
    }
}
