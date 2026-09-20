using Microsoft.AspNetCore.Mvc;
using MobilityFinance.Contracts;
using MobilityFinance.Messaging;
using MobilityFinance.Origination.Api.Infrastructure;
using MobilityFinance.Origination.Domain;

namespace MobilityFinance.Origination.Api.Applications;

public static class OriginationEndpoints
{
    public static IEndpointRouteBuilder MapOriginationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder applications = endpoints
            .MapGroup("/applications")
            .WithTags("Origination");

        applications.MapGet(
            "/",
            (InMemoryOriginationRepository repository) =>
                Results.Ok(repository.List().Select(ToResponse)));

        applications.MapGet(
            "/{id:guid}",
            (Guid id, InMemoryOriginationRepository repository) =>
            {
                FinancingApplication? application = repository.Get(id);
                return application is null
                    ? Results.NotFound()
                    : Results.Ok(ToResponse(application));
            });

        applications.MapPost(
            "/",
            (
                SubmitApplicationRequest request,
                InMemoryOriginationRepository repository,
                TimeProvider timeProvider) =>
            {
                try
                {
                    DateTimeOffset now = timeProvider.GetUtcNow();
                    FinancingApplication application = FinancingApplication.Submit(
                        request.ApplicantReference,
                        request.Market,
                        new Money(request.AssetPrice, request.Currency),
                        new Money(request.Deposit, request.Currency),
                        request.TermWeeks,
                        new Money(request.WeeklyIncome, request.Currency),
                        now);
                    application.Price(PricingPolicy.KenyaV1, now);
                    repository.Add(application);

                    return Results.Created(
                        $"/applications/{application.Id}",
                        ToResponse(application));
                }
                catch (PricingDeclinedException exception)
                {
                    return Results.Problem(
                        statusCode: StatusCodes.Status422UnprocessableEntity,
                        title: "Pricing declined",
                        detail: exception.Message);
                }
                catch (ArgumentException exception)
                {
                    return Results.ValidationProblem(
                        new Dictionary<string, string[]>
                        {
                            ["application"] = [exception.Message],
                        });
                }
            });

        applications.MapPost(
            "/{id:guid}/accept",
            (
                Guid id,
                AcceptOfferRequest request,
                InMemoryOriginationRepository repository,
                TimeProvider timeProvider,
                InMemoryEventOutbox outbox) =>
            {
                try
                {
                    DateTimeOffset now = timeProvider.GetUtcNow();
                    FinancingAgreement agreement = repository.Update(
                        id,
                        application => application.AcceptOffer(
                            request.OfferId,
                            now));
                    outbox.Enqueue(
                        IntegrationEventFactory.Create(
                            IntegrationEventTypes.AgreementCreated,
                            agreement.Id.ToString(),
                            aggregateSequence: 1,
                            agreement.Id,
                            causationId: null,
                            now,
                            new AgreementCreatedEvent(
                                agreement.Id,
                                agreement.ApplicationId,
                                agreement.ApplicantReference,
                                agreement.DepositRequired.Amount,
                                agreement.Currency)));

                    return Results.Ok(ToResponse(agreement));
                }
                catch (KeyNotFoundException)
                {
                    return Results.NotFound();
                }
                catch (InvalidOperationException exception)
                {
                    return Results.Conflict(
                        new ProblemDetails
                        {
                            Status = StatusCodes.Status409Conflict,
                            Title = "Offer cannot be accepted",
                            Detail = exception.Message,
                        });
                }
            });

        return endpoints;
    }

    private static ApplicationResponse ToResponse(FinancingApplication application)
    {
        return new ApplicationResponse(
            application.Id,
            application.ApplicantReference,
            application.Market,
            application.Status.ToString(),
            application.AssetPrice,
            application.Deposit,
            application.TermWeeks,
            application.WeeklyIncome,
            application.SubmittedAtUtc,
            application.Offer is null ? null : ToResponse(application.Offer),
            application.Agreement is null ? null : ToResponse(application.Agreement));
    }

    private static OfferResponse ToResponse(FinancingOffer offer)
    {
        return new OfferResponse(
            offer.Id,
            offer.PolicyVersion,
            offer.Principal,
            offer.FinanceCharge,
            offer.ServiceFee,
            offer.TotalRepayable,
            offer.TermWeeks,
            offer.PricedAtUtc,
            offer.ExpiresAtUtc,
            offer.Schedule);
    }

    private static AgreementResponse ToResponse(FinancingAgreement agreement)
    {
        return new AgreementResponse(
            agreement.Id,
            agreement.ApplicationId,
            agreement.OfferId,
            agreement.PolicyVersion,
            agreement.DepositRequired,
            agreement.TotalRepayable,
            agreement.CreatedAtUtc);
    }
}

public sealed record SubmitApplicationRequest(
    string ApplicantReference,
    string Market,
    string Currency,
    decimal AssetPrice,
    decimal Deposit,
    int TermWeeks,
    decimal WeeklyIncome);

public sealed record AcceptOfferRequest(Guid OfferId);

public sealed record ApplicationResponse(
    Guid Id,
    string ApplicantReference,
    string Market,
    string Status,
    Money AssetPrice,
    Money Deposit,
    int TermWeeks,
    Money WeeklyIncome,
    DateTimeOffset SubmittedAtUtc,
    OfferResponse? Offer,
    AgreementResponse? Agreement);

public sealed record OfferResponse(
    Guid Id,
    string PolicyVersion,
    Money Principal,
    Money FinanceCharge,
    Money ServiceFee,
    Money TotalRepayable,
    int TermWeeks,
    DateTimeOffset PricedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    IReadOnlyList<RepaymentInstallment> Schedule);

public sealed record AgreementResponse(
    Guid Id,
    Guid ApplicationId,
    Guid OfferId,
    string PolicyVersion,
    Money DepositRequired,
    Money TotalRepayable,
    DateTimeOffset CreatedAtUtc);
