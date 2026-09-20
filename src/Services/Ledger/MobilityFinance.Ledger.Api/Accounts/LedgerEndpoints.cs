using Microsoft.AspNetCore.Mvc;
using MobilityFinance.Ledger.Api.Infrastructure;
using MobilityFinance.Ledger.Domain;

namespace MobilityFinance.Ledger.Api.Accounts;

public static class LedgerEndpoints
{
    public static IEndpointRouteBuilder MapLedgerEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder accounts = endpoints.MapGroup("/accounts");

        accounts.MapGet(
            "/",
            (InMemoryLedgerRepository repository) =>
                Results.Ok(repository.List().Select(MapAccount)));

        accounts.MapGet(
            "/{id:guid}",
            (Guid id, InMemoryLedgerRepository repository) =>
            {
                FinancingLedgerAccount? account = repository.Get(id);
                return account is null
                    ? Results.NotFound()
                    : Results.Ok(MapAccount(account));
            });

        accounts.MapPost(
            "/",
            (
                OpenLedgerAccountRequest request,
                InMemoryLedgerRepository repository,
                TimeProvider timeProvider) =>
            {
                try
                {
                    FinancingLedgerAccount account =
                        FinancingLedgerAccount.Open(
                            request.AgreementId,
                            request.CustomerReference,
                            Money(request.DepositRequired, request.Currency),
                            Money(request.Principal, request.Currency),
                            Money(request.FinanceCharge, request.Currency),
                            Money(request.ServiceFee, request.Currency),
                            timeProvider.GetUtcNow());
                    FinancingLedgerAccount stored =
                        repository.AddOrGet(account);

                    return Results.Created(
                        $"/accounts/{stored.Id}",
                        MapAccount(stored));
                }
                catch (ArgumentException exception)
                {
                    return ValidationProblem(exception);
                }
                catch (InvalidOperationException exception)
                {
                    return Results.Conflict(Problem(exception.Message));
                }
            });

        accounts.MapPost(
            "/{id:guid}/payments",
            (
                Guid id,
                RecordPaymentRequest request,
                InMemoryLedgerRepository repository,
                TimeProvider timeProvider) =>
            {
                try
                {
                    PaymentReceipt receipt = repository.Update(
                        id,
                        account => account.RecordPayment(
                            request.Provider,
                            request.ProviderTransactionId,
                            Money(request.Amount, request.Currency),
                            timeProvider.GetUtcNow()));
                    PaymentResponse response = MapPayment(receipt.Payment);

                    return receipt.IsDuplicate
                        ? Results.Ok(
                            new PaymentReceiptResponse(
                                response,
                                IsDuplicate: true))
                        : Results.Created(
                            $"/accounts/{id}/payments/{receipt.Payment.Id}",
                            new PaymentReceiptResponse(
                                response,
                                IsDuplicate: false));
                }
                catch (KeyNotFoundException)
                {
                    return Results.NotFound();
                }
                catch (ArgumentException exception)
                {
                    return ValidationProblem(exception);
                }
                catch (InvalidOperationException exception)
                {
                    return Results.Conflict(Problem(exception.Message));
                }
            });

        accounts.MapPost(
            "/{id:guid}/payments/{paymentId:guid}/reverse",
            (
                Guid id,
                Guid paymentId,
                ReversePaymentRequest request,
                InMemoryLedgerRepository repository,
                TimeProvider timeProvider) =>
            {
                try
                {
                    PaymentRecord payment = repository.Update(
                        id,
                        account => account.ReversePayment(
                            paymentId,
                            request.Reason,
                            timeProvider.GetUtcNow()));

                    return Results.Ok(MapPayment(payment));
                }
                catch (KeyNotFoundException)
                {
                    return Results.NotFound();
                }
                catch (ArgumentException exception)
                {
                    return ValidationProblem(exception);
                }
                catch (InvalidOperationException exception)
                {
                    return Results.Conflict(Problem(exception.Message));
                }
            });

        return endpoints;
    }

    private static LedgerAccountResponse MapAccount(
        FinancingLedgerAccount account)
    {
        return new LedgerAccountResponse(
            account.Id,
            account.AgreementId,
            account.CustomerReference,
            account.OpenedAtUtc,
            MapMoney(account.DepositRequired),
            MapMoney(account.Principal),
            MapMoney(account.FinanceCharge),
            MapMoney(account.ServiceFee),
            new LedgerBalanceResponse(
                MapMoney(account.Balance.DepositOutstanding),
                MapMoney(account.Balance.RepaymentOutstanding),
                MapMoney(account.Balance.CustomerCredit),
                account.Balance.DepositSatisfied,
                account.Balance.Settled),
            account.Payments.Select(MapPayment).ToArray(),
            account.Transactions.Select(MapTransaction).ToArray());
    }

    private static PaymentResponse MapPayment(PaymentRecord payment)
    {
        return new PaymentResponse(
            payment.Id,
            payment.Provider,
            payment.ProviderTransactionId,
            MapMoney(payment.Amount),
            MapMoney(payment.DepositApplied),
            MapMoney(payment.RepaymentApplied),
            MapMoney(payment.CustomerCredit),
            payment.ReceivedAtUtc,
            payment.Status.ToString(),
            payment.LedgerTransactionId,
            payment.ReversalTransactionId);
    }

    private static LedgerTransactionResponse MapTransaction(
        LedgerTransaction transaction)
    {
        return new LedgerTransactionResponse(
            transaction.Id,
            transaction.Type.ToString(),
            transaction.Reference,
            transaction.PostedAtUtc,
            transaction.Entries
                .Select(
                    entry => new LedgerEntryResponse(
                        entry.Account.ToString(),
                        entry.Direction.ToString(),
                        MapMoney(entry.Amount)))
                .ToArray());
    }

    private static LedgerMoney Money(decimal amount, string currency) =>
        new(amount, currency);

    private static MoneyResponse MapMoney(LedgerMoney money) =>
        new(money.Amount, money.Currency);

    private static IResult ValidationProblem(ArgumentException exception)
    {
        return Results.ValidationProblem(
            new Dictionary<string, string[]>
            {
                [exception.ParamName ?? "request"] = [exception.Message],
            });
    }

    private static ProblemDetails Problem(string detail) =>
        new()
        {
            Title = "Ledger request conflict",
            Detail = detail,
            Status = StatusCodes.Status409Conflict,
        };
}

public sealed record OpenLedgerAccountRequest(
    Guid AgreementId,
    string CustomerReference,
    string Currency,
    decimal DepositRequired,
    decimal Principal,
    decimal FinanceCharge,
    decimal ServiceFee);

public sealed record RecordPaymentRequest(
    string Provider,
    string ProviderTransactionId,
    string Currency,
    decimal Amount);

public sealed record ReversePaymentRequest(string Reason);

public sealed record LedgerAccountResponse(
    Guid Id,
    Guid AgreementId,
    string CustomerReference,
    DateTimeOffset OpenedAtUtc,
    MoneyResponse DepositRequired,
    MoneyResponse Principal,
    MoneyResponse FinanceCharge,
    MoneyResponse ServiceFee,
    LedgerBalanceResponse Balance,
    IReadOnlyList<PaymentResponse> Payments,
    IReadOnlyList<LedgerTransactionResponse> Transactions);

public sealed record LedgerBalanceResponse(
    MoneyResponse DepositOutstanding,
    MoneyResponse RepaymentOutstanding,
    MoneyResponse CustomerCredit,
    bool DepositSatisfied,
    bool Settled);

public sealed record PaymentReceiptResponse(
    PaymentResponse Payment,
    bool IsDuplicate);

public sealed record PaymentResponse(
    Guid Id,
    string Provider,
    string ProviderTransactionId,
    MoneyResponse Amount,
    MoneyResponse DepositApplied,
    MoneyResponse RepaymentApplied,
    MoneyResponse CustomerCredit,
    DateTimeOffset ReceivedAtUtc,
    string Status,
    Guid LedgerTransactionId,
    Guid? ReversalTransactionId);

public sealed record LedgerTransactionResponse(
    Guid Id,
    string Type,
    string Reference,
    DateTimeOffset PostedAtUtc,
    IReadOnlyList<LedgerEntryResponse> Entries);

public sealed record LedgerEntryResponse(
    string Account,
    string Direction,
    MoneyResponse Amount);

public sealed record MoneyResponse(decimal Amount, string Currency);
