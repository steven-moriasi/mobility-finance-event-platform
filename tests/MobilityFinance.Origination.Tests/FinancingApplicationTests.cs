using MobilityFinance.Origination.Domain;

namespace MobilityFinance.Origination.Tests;

public sealed class FinancingApplicationTests
{
    private static readonly DateTimeOffset SubmittedAt =
        new(2026, 1, 15, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AcceptedOfferCreatesAnAgreementFromThePricingSnapshot()
    {
        FinancingApplication application = CreateApplication();
        FinancingOffer offer = application.Price(PricingPolicy.KenyaV1, SubmittedAt);

        FinancingAgreement agreement = application.AcceptOffer(
            offer.Id,
            SubmittedAt.AddHours(2));

        Assert.Equal(ApplicationStatus.Accepted, application.Status);
        Assert.Equal(offer.PolicyVersion, agreement.PolicyVersion);
        Assert.Equal(offer.TotalRepayable, agreement.TotalRepayable);
        Assert.Equal(offer.Schedule, agreement.Schedule);
    }

    [Fact]
    public void ExpiredOfferCannotBeAccepted()
    {
        FinancingApplication application = CreateApplication();
        FinancingOffer offer = application.Price(PricingPolicy.KenyaV1, SubmittedAt);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => application.AcceptOffer(
                offer.Id,
                offer.ExpiresAtUtc.AddSeconds(1)));

        Assert.Equal("The offer has expired.", exception.Message);
        Assert.Equal(ApplicationStatus.Priced, application.Status);
    }

    [Fact]
    public void AcceptedApplicationCannotBeRepriced()
    {
        FinancingApplication application = CreateApplication();
        FinancingOffer offer = application.Price(PricingPolicy.KenyaV1, SubmittedAt);
        application.AcceptOffer(offer.Id, SubmittedAt.AddHours(1));

        Assert.Throws<InvalidOperationException>(
            () => application.Price(
                PricingPolicy.KenyaV1,
                SubmittedAt.AddHours(2)));
    }

    private static FinancingApplication CreateApplication()
    {
        return FinancingApplication.Submit(
            "SYNTH-1001",
            "KE",
            new Money(250_000m, "KES"),
            new Money(30_000m, "KES"),
            52,
            new Money(12_000m, "KES"),
            SubmittedAt);
    }
}
