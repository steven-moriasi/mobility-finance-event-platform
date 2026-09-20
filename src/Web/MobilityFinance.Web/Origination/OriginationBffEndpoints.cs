using Microsoft.AspNetCore.Mvc;
using MobilityFinance.Web.Security;

namespace MobilityFinance.Web.Origination;

public static class OriginationBffEndpoints
{
    public static IEndpointRouteBuilder MapOriginationBff(
        this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints
            .MapGroup("/bff/origination")
            .RequireAuthorization(AuthorizationPolicies.OperatorWork)
            .ExcludeFromDescription();

        group.MapGet(
            "/applications",
            async (
                OriginationClient client,
                CancellationToken cancellationToken) =>
            {
                IReadOnlyList<FinancingApplicationDto> applications =
                    await client.ListApplicationsAsync(cancellationToken);
                return Results.Ok(applications);
            });

        group.MapPost(
            "/applications",
            async (
                [FromForm] CreateApplicationForm form,
                OriginationClient client,
                CancellationToken cancellationToken) =>
            {
                OriginationResult<FinancingApplicationDto> result =
                    await client.SubmitApplicationAsync(
                        new CreateApplicationCommand(
                            form.ApplicantReference,
                            "KE",
                            "KES",
                            form.AssetPrice,
                            form.Deposit,
                            form.TermWeeks,
                            form.WeeklyIncome),
                        cancellationToken);

                return result.IsSuccess
                    ? Results.LocalRedirect(
                        $"/applications?created={result.Value!.Id}")
                    : Results.LocalRedirect(
                        $"/applications?error={Uri.EscapeDataString(result.Error!)}");
            });

        group.MapPost(
            "/applications/{applicationId:guid}/accept",
            async (
                Guid applicationId,
                [FromForm] AcceptOfferForm form,
                OriginationClient client,
                CancellationToken cancellationToken) =>
            {
                OriginationResult<FinancingAgreementDto> result =
                    await client.AcceptOfferAsync(
                        applicationId,
                        form.OfferId,
                        cancellationToken);

                return result.IsSuccess
                    ? Results.LocalRedirect(
                        $"/applications?accepted={result.Value!.Id}")
                    : Results.LocalRedirect(
                        $"/applications?error={Uri.EscapeDataString(result.Error!)}");
            });

        return endpoints;
    }
}

public sealed record CreateApplicationForm(
    string ApplicantReference,
    decimal AssetPrice,
    decimal Deposit,
    int TermWeeks,
    decimal WeeklyIncome);

public sealed record AcceptOfferForm(Guid OfferId);
