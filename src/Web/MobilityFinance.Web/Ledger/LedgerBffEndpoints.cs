using Microsoft.AspNetCore.Mvc;
using MobilityFinance.Web.Origination;
using MobilityFinance.Web.Security;

namespace MobilityFinance.Web.Ledger;

public static class LedgerBffEndpoints
{
    public static IEndpointRouteBuilder MapLedgerBff(
        this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder operatorGroup = endpoints
            .MapGroup("/bff/ledger")
            .RequireAuthorization(AuthorizationPolicies.OperatorWork)
            .ExcludeFromDescription();

        operatorGroup.MapGet(
            "/accounts",
            async (
                LedgerClient client,
                CancellationToken cancellationToken) =>
            {
                IReadOnlyList<LedgerAccountDto> accounts =
                    await client.ListAccountsAsync(cancellationToken);
                return Results.Ok(accounts);
            });

        operatorGroup.MapPost(
            "/accounts",
            async (
                [FromForm] OpenLedgerAccountForm form,
                OriginationClient origination,
                LedgerClient ledger,
                CancellationToken cancellationToken) =>
            {
                FinancingApplicationDto? application =
                    await origination.GetApplicationAsync(
                        form.ApplicationId,
                        cancellationToken);
                if (application?.Agreement is null || application.Offer is null)
                {
                    return RedirectWithError(
                        "/agreements",
                        "Accepted agreement terms were not found.");
                }

                LedgerResult<LedgerAccountDto> result =
                    await ledger.OpenAccountAsync(
                        new OpenLedgerAccountCommand(
                            application.Agreement.Id,
                            application.ApplicantReference,
                            application.Offer.TotalRepayable.Currency,
                            application.Agreement.DepositRequired.Amount,
                            application.Offer.Principal.Amount,
                            application.Offer.FinanceCharge.Amount,
                            application.Offer.ServiceFee.Amount),
                        cancellationToken);

                return result.IsSuccess
                    ? Results.LocalRedirect(
                        $"/repayments?opened={result.Value!.Id}")
                    : RedirectWithError("/agreements", result.Error!);
            });

        operatorGroup.MapPost(
            "/accounts/{accountId:guid}/payments",
            async (
                Guid accountId,
                [FromForm] RecordPaymentForm form,
                LedgerClient ledger,
                CancellationToken cancellationToken) =>
            {
                LedgerResult<PaymentReceiptDto> result =
                    await ledger.RecordPaymentAsync(
                        accountId,
                        new RecordPaymentCommand(
                            "SYNTHETIC-PROVIDER",
                            form.ProviderTransactionId,
                            "KES",
                            form.Amount),
                        cancellationToken);

                return result.IsSuccess
                    ? Results.LocalRedirect(
                        $"/repayments?payment={result.Value!.Payment.Id}&duplicate={result.Value.IsDuplicate}")
                    : RedirectWithError("/repayments", result.Error!);
            });

        endpoints
            .MapPost(
                "/bff/ledger/accounts/{accountId:guid}/payments/{paymentId:guid}/reverse",
                async (
                    Guid accountId,
                    Guid paymentId,
                    [FromForm] ReversePaymentForm form,
                    LedgerClient ledger,
                    CancellationToken cancellationToken) =>
                {
                    LedgerResult<PaymentDto> result =
                        await ledger.ReversePaymentAsync(
                            accountId,
                            paymentId,
                            form.Reason,
                            cancellationToken);

                    return result.IsSuccess
                        ? Results.LocalRedirect(
                            $"/repayments?reversed={result.Value!.Id}")
                        : RedirectWithError("/repayments", result.Error!);
                })
            .RequireAuthorization(AuthorizationPolicies.ReviewWork)
            .ExcludeFromDescription();

        return endpoints;
    }

    private static IResult RedirectWithError(string path, string error)
    {
        return Results.LocalRedirect(
            $"{path}?error={Uri.EscapeDataString(error)}");
    }
}

public sealed record OpenLedgerAccountForm(Guid ApplicationId);

public sealed record RecordPaymentForm(
    string ProviderTransactionId,
    decimal Amount);

public sealed record ReversePaymentForm(string Reason);
