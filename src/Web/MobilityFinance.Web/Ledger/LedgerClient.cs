using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;

namespace MobilityFinance.Web.Ledger;

public sealed class LedgerClient(HttpClient httpClient)
{
    public async Task<IReadOnlyList<LedgerAccountDto>> ListAccountsAsync(
        CancellationToken cancellationToken = default)
    {
        LedgerAccountDto[]? accounts =
            await httpClient.GetFromJsonAsync<LedgerAccountDto[]>(
                "accounts",
                cancellationToken);
        return accounts ?? [];
    }

    public async Task<LedgerResult<LedgerAccountDto>> OpenAccountAsync(
        OpenLedgerAccountCommand command,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync(
            "accounts",
            command,
            cancellationToken);
        return await ReadResultAsync<LedgerAccountDto>(
            response,
            cancellationToken);
    }

    public async Task<LedgerResult<PaymentReceiptDto>> RecordPaymentAsync(
        Guid accountId,
        RecordPaymentCommand command,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync(
            $"accounts/{accountId}/payments",
            command,
            cancellationToken);
        return await ReadResultAsync<PaymentReceiptDto>(
            response,
            cancellationToken);
    }

    public async Task<LedgerResult<PaymentDto>> ReversePaymentAsync(
        Guid accountId,
        Guid paymentId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync(
            $"accounts/{accountId}/payments/{paymentId}/reverse",
            new ReversePaymentCommand(reason),
            cancellationToken);
        return await ReadResultAsync<PaymentDto>(
            response,
            cancellationToken);
    }

    private static async Task<LedgerResult<T>> ReadResultAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            T? value = await response.Content.ReadFromJsonAsync<T>(
                cancellationToken);
            return value is null
                ? LedgerResult.Failed<T>("Ledger returned an empty response.")
                : LedgerResult.Succeeded(value);
        }

        ProblemDetails? problem =
            await response.Content.ReadFromJsonAsync<ProblemDetails>(
                cancellationToken);
        return LedgerResult.Failed<T>(
            problem?.Detail ?? problem?.Title ?? "Ledger request failed.");
    }
}

public sealed record OpenLedgerAccountCommand(
    Guid AgreementId,
    string CustomerReference,
    string Currency,
    decimal DepositRequired,
    decimal Principal,
    decimal FinanceCharge,
    decimal ServiceFee);

public sealed record RecordPaymentCommand(
    string Provider,
    string ProviderTransactionId,
    string Currency,
    decimal Amount);

public sealed record ReversePaymentCommand(string Reason);

public sealed record LedgerResult<T>(T? Value, string? Error)
{
    public bool IsSuccess => Value is not null;
}

public static class LedgerResult
{
    public static LedgerResult<T> Succeeded<T>(T value) => new(value, null);

    public static LedgerResult<T> Failed<T>(string error) => new(default, error);
}

public sealed record LedgerAccountDto(
    Guid Id,
    Guid AgreementId,
    string CustomerReference,
    DateTimeOffset OpenedAtUtc,
    LedgerMoneyDto DepositRequired,
    LedgerMoneyDto Principal,
    LedgerMoneyDto FinanceCharge,
    LedgerMoneyDto ServiceFee,
    LedgerBalanceDto Balance,
    IReadOnlyList<PaymentDto> Payments,
    IReadOnlyList<LedgerTransactionDto> Transactions);

public sealed record LedgerBalanceDto(
    LedgerMoneyDto DepositOutstanding,
    LedgerMoneyDto RepaymentOutstanding,
    LedgerMoneyDto CustomerCredit,
    bool DepositSatisfied,
    bool Settled);

public sealed record PaymentReceiptDto(
    PaymentDto Payment,
    bool IsDuplicate);

public sealed record PaymentDto(
    Guid Id,
    string Provider,
    string ProviderTransactionId,
    LedgerMoneyDto Amount,
    LedgerMoneyDto DepositApplied,
    LedgerMoneyDto RepaymentApplied,
    LedgerMoneyDto CustomerCredit,
    DateTimeOffset ReceivedAtUtc,
    string Status,
    Guid LedgerTransactionId,
    Guid? ReversalTransactionId);

public sealed record LedgerTransactionDto(
    Guid Id,
    string Type,
    string Reference,
    DateTimeOffset PostedAtUtc,
    IReadOnlyList<LedgerEntryDto> Entries);

public sealed record LedgerEntryDto(
    string Account,
    string Direction,
    LedgerMoneyDto Amount);

public sealed record LedgerMoneyDto(decimal Amount, string Currency);
