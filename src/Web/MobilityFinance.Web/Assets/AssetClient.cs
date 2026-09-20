using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;

namespace MobilityFinance.Web.Assets;

public sealed class AssetClient(HttpClient httpClient)
{
    public async Task<IReadOnlyList<AssetDto>> ListAssetsAsync(
        CancellationToken cancellationToken = default)
    {
        AssetDto[]? assets = await httpClient.GetFromJsonAsync<AssetDto[]>(
            "assets",
            cancellationToken);
        return assets ?? [];
    }

    public async Task<AssetResult<AssetDto>> RegisterAssetAsync(
        RegisterAssetCommand command,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync(
            "assets",
            command,
            cancellationToken);
        return await ReadResultAsync<AssetDto>(response, cancellationToken);
    }

    public async Task<AssetResult<AssetAssignmentDto>> AssignAssetAsync(
        Guid assetId,
        Guid agreementId,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync(
            $"assets/{assetId}/assign",
            new AssignAssetCommand(agreementId),
            cancellationToken);
        return await ReadResultAsync<AssetAssignmentDto>(
            response,
            cancellationToken);
    }

    public async Task<AssetResult<AssetDto>> SimulateDeviceAsync(
        Guid assetId,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.PostAsync(
            $"assets/{assetId}/simulation/tick",
            content: null,
            cancellationToken);
        return await ReadResultAsync<AssetDto>(response, cancellationToken);
    }

    public async Task<AssetResult<DeviceCommandDto>> QueueCommandAsync(
        Guid assetId,
        QueueDeviceCommand command,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync(
            $"assets/{assetId}/commands",
            command,
            cancellationToken);
        return await ReadResultAsync<DeviceCommandDto>(
            response,
            cancellationToken);
    }

    private static async Task<AssetResult<T>> ReadResultAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            T? value = await response.Content.ReadFromJsonAsync<T>(
                cancellationToken);
            return value is null
                ? AssetResult.Failed<T>("Asset service returned an empty response.")
                : AssetResult.Succeeded(value);
        }

        ProblemDetails? problem =
            await response.Content.ReadFromJsonAsync<ProblemDetails>(
                cancellationToken);
        return AssetResult.Failed<T>(
            problem?.Detail ?? problem?.Title ?? "Asset request failed.");
    }
}

public sealed record RegisterAssetCommand(
    string Reference,
    string SerialNumber,
    string Model,
    string Market);

public sealed record AssignAssetCommand(Guid AgreementId);

public sealed record QueueDeviceCommand(int Type, string Reason);

public sealed record AssetResult<T>(T? Value, string? Error)
{
    public bool IsSuccess => Value is not null;
}

public static class AssetResult
{
    public static AssetResult<T> Succeeded<T>(T value) => new(value, null);

    public static AssetResult<T> Failed<T>(string error) => new(default, error);
}

public sealed record AssetAssignmentDto(
    AssetDto Asset,
    bool IsDuplicate);

public sealed record AssetDto(
    Guid Id,
    string Reference,
    string SerialNumber,
    string Model,
    string Market,
    DateTimeOffset RegisteredAtUtc,
    string Status,
    Guid? AgreementId,
    long Version,
    HeartbeatDto? LastHeartbeat,
    IReadOnlyList<DeviceCommandDto> Commands);

public sealed record HeartbeatDto(
    DateTimeOffset RecordedAtUtc,
    decimal BatteryPercent,
    decimal OdometerKm,
    decimal Latitude,
    decimal Longitude);

public sealed record DeviceCommandDto(
    Guid Id,
    string Type,
    string Reason,
    DateTimeOffset RequestedAtUtc,
    string Status,
    DateTimeOffset? CompletedAtUtc,
    string? Result);
