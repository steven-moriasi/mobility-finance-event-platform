using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;

namespace MobilityFinance.Web.Origination;

public sealed class OriginationClient(HttpClient httpClient)
{
    public async Task<IReadOnlyList<FinancingApplicationDto>> ListApplicationsAsync(
        CancellationToken cancellationToken = default)
    {
        FinancingApplicationDto[]? applications =
            await httpClient.GetFromJsonAsync<FinancingApplicationDto[]>(
                "applications",
                cancellationToken);

        return applications ?? [];
    }

    public Task<FinancingApplicationDto?> GetApplicationAsync(
        Guid applicationId,
        CancellationToken cancellationToken = default)
    {
        return httpClient.GetFromJsonAsync<FinancingApplicationDto>(
            $"applications/{applicationId}",
            cancellationToken);
    }

    public async Task<OriginationResult<FinancingApplicationDto>> SubmitApplicationAsync(
        CreateApplicationCommand command,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync(
            "applications",
            command,
            cancellationToken);

        return await ReadResultAsync<FinancingApplicationDto>(
            response,
            cancellationToken);
    }

    public async Task<OriginationResult<FinancingAgreementDto>> AcceptOfferAsync(
        Guid applicationId,
        Guid offerId,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync(
            $"applications/{applicationId}/accept",
            new AcceptOfferCommand(offerId),
            cancellationToken);

        return await ReadResultAsync<FinancingAgreementDto>(
            response,
            cancellationToken);
    }

    private static async Task<OriginationResult<T>> ReadResultAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            T? value = await response.Content.ReadFromJsonAsync<T>(
                cancellationToken);
            return value is null
                ? OriginationResult.Failed<T>("Origination returned an empty response.")
                : OriginationResult.Succeeded(value);
        }

        ProblemDetails? problem =
            await response.Content.ReadFromJsonAsync<ProblemDetails>(
                cancellationToken);
        return OriginationResult.Failed<T>(
            problem?.Detail ?? problem?.Title ?? "Origination request failed.");
    }
}

public sealed record CreateApplicationCommand(
    string ApplicantReference,
    string Market,
    string Currency,
    decimal AssetPrice,
    decimal Deposit,
    int TermWeeks,
    decimal WeeklyIncome);

public sealed record AcceptOfferCommand(Guid OfferId);

public sealed record OriginationResult<T>(T? Value, string? Error)
{
    public bool IsSuccess => Value is not null;
}

public static class OriginationResult
{
    public static OriginationResult<T> Succeeded<T>(T value) => new(value, null);

    public static OriginationResult<T> Failed<T>(string error) => new(default, error);
}

public sealed record FinancingApplicationDto(
    Guid Id,
    string ApplicantReference,
    string Market,
    string Status,
    MoneyDto AssetPrice,
    MoneyDto Deposit,
    int TermWeeks,
    MoneyDto WeeklyIncome,
    DateTimeOffset SubmittedAtUtc,
    FinancingOfferDto? Offer,
    FinancingAgreementDto? Agreement);

public sealed record FinancingOfferDto(
    Guid Id,
    string PolicyVersion,
    MoneyDto Principal,
    MoneyDto FinanceCharge,
    MoneyDto ServiceFee,
    MoneyDto TotalRepayable,
    int TermWeeks,
    DateTimeOffset PricedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    IReadOnlyList<RepaymentInstallmentDto> Schedule);

public sealed record RepaymentInstallmentDto(
    int Sequence,
    DateTimeOffset DueAtUtc,
    MoneyDto Amount);

public sealed record FinancingAgreementDto(
    Guid Id,
    Guid ApplicationId,
    Guid OfferId,
    string PolicyVersion,
    MoneyDto DepositRequired,
    MoneyDto TotalRepayable,
    DateTimeOffset CreatedAtUtc);

public sealed record MoneyDto(decimal Amount, string Currency);
