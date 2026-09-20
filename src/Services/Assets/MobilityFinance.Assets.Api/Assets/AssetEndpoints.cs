using Microsoft.AspNetCore.Mvc;
using MobilityFinance.Assets.Api.Infrastructure;
using MobilityFinance.Assets.Domain;
using MobilityFinance.Contracts;
using MobilityFinance.Messaging;

namespace MobilityFinance.Assets.Api.Assets;

public static class AssetEndpoints
{
    public static IEndpointRouteBuilder MapAssetEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder assets = endpoints.MapGroup("/assets");

        assets.MapGet(
            "/",
            (InMemoryAssetRepository repository) =>
                Results.Ok(repository.List().Select(MapAsset)));

        assets.MapGet(
            "/{id:guid}",
            (Guid id, InMemoryAssetRepository repository) =>
            {
                MobilityAsset? asset = repository.Get(id);
                return asset is null
                    ? Results.NotFound()
                    : Results.Ok(MapAsset(asset));
            });

        assets.MapPost(
            "/",
            (
                RegisterAssetRequest request,
                InMemoryAssetRepository repository,
                TimeProvider timeProvider) =>
            {
                try
                {
                    MobilityAsset asset = MobilityAsset.Register(
                        request.Reference,
                        request.SerialNumber,
                        request.Model,
                        request.Market,
                        timeProvider.GetUtcNow());
                    repository.Add(asset);
                    return Results.Created(
                        $"/assets/{asset.Id}",
                        MapAsset(asset));
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

        assets.MapPost(
            "/{id:guid}/assign",
            (
                Guid id,
                AssignAssetRequest request,
                InMemoryAssetRepository repository,
                InMemoryEventOutbox outbox,
                TimeProvider timeProvider) =>
            {
                try
                {
                    bool assigned = repository.Update(
                        id,
                        asset => asset.Assign(request.AgreementId));
                    MobilityAsset asset = repository.Get(id)!;
                    if (assigned)
                    {
                        outbox.Enqueue(
                            IntegrationEventFactory.Create(
                                IntegrationEventTypes.AssetAssigned,
                                request.AgreementId.ToString(),
                                asset.Version,
                                request.AgreementId,
                                causationId: null,
                                timeProvider.GetUtcNow(),
                                new AssetAssignedEvent(
                                    request.AgreementId,
                                    asset.Id,
                                    asset.Reference),
                                asset.Market));
                    }

                    return Results.Ok(
                        new AssetAssignmentResponse(
                            MapAsset(asset),
                            IsDuplicate: !assigned));
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

        assets.MapPost(
            "/{id:guid}/heartbeats",
            (
                Guid id,
                RecordHeartbeatRequest request,
                InMemoryAssetRepository repository) =>
            {
                try
                {
                    repository.Update(
                        id,
                        asset =>
                        {
                            asset.RecordHeartbeat(
                                new AssetHeartbeat(
                                    request.RecordedAtUtc,
                                    request.BatteryPercent,
                                    request.OdometerKm,
                                    request.Latitude,
                                    request.Longitude));
                            return asset;
                        });
                    return Results.Ok(MapAsset(repository.Get(id)!));
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

        assets.MapPost(
            "/{id:guid}/commands",
            (
                Guid id,
                QueueDeviceCommandRequest request,
                InMemoryAssetRepository repository,
                TimeProvider timeProvider) =>
            {
                try
                {
                    DeviceCommand command = repository.Update(
                        id,
                        asset => asset.QueueCommand(
                            request.Type,
                            request.Reason,
                            timeProvider.GetUtcNow()));
                    return Results.Accepted(
                        $"/assets/{id}",
                        MapCommand(command));
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

        assets.MapPost(
            "/{id:guid}/simulation/tick",
            (
                Guid id,
                InMemoryAssetRepository repository,
                TimeProvider timeProvider) =>
            {
                try
                {
                    repository.Update(
                        id,
                        asset => asset.SimulateDevice(
                            timeProvider.GetUtcNow()));
                    return Results.Ok(MapAsset(repository.Get(id)!));
                }
                catch (KeyNotFoundException)
                {
                    return Results.NotFound();
                }
                catch (InvalidOperationException exception)
                {
                    return Results.Conflict(Problem(exception.Message));
                }
            });

        return endpoints;
    }

    private static AssetResponse MapAsset(MobilityAsset asset)
    {
        return new AssetResponse(
            asset.Id,
            asset.Reference,
            asset.SerialNumber,
            asset.Model,
            asset.Market,
            asset.RegisteredAtUtc,
            asset.Status.ToString(),
            asset.AgreementId,
            asset.Version,
            asset.LastHeartbeat is null
                ? null
                : new HeartbeatResponse(
                    asset.LastHeartbeat.RecordedAtUtc,
                    asset.LastHeartbeat.BatteryPercent,
                    asset.LastHeartbeat.OdometerKm,
                    asset.LastHeartbeat.Latitude,
                    asset.LastHeartbeat.Longitude),
            asset.Commands.Select(MapCommand).ToArray());
    }

    private static DeviceCommandResponse MapCommand(DeviceCommand command)
    {
        return new DeviceCommandResponse(
            command.Id,
            command.Type.ToString(),
            command.Reason,
            command.RequestedAtUtc,
            command.Status.ToString(),
            command.CompletedAtUtc,
            command.Result);
    }

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
            Title = "Asset request conflict",
            Detail = detail,
            Status = StatusCodes.Status409Conflict,
        };
}

public sealed record RegisterAssetRequest(
    string Reference,
    string SerialNumber,
    string Model,
    string Market);

public sealed record AssignAssetRequest(Guid AgreementId);

public sealed record RecordHeartbeatRequest(
    DateTimeOffset RecordedAtUtc,
    decimal BatteryPercent,
    decimal OdometerKm,
    decimal Latitude,
    decimal Longitude);

public sealed record QueueDeviceCommandRequest(
    DeviceCommandType Type,
    string Reason);

public sealed record AssetAssignmentResponse(
    AssetResponse Asset,
    bool IsDuplicate);

public sealed record AssetResponse(
    Guid Id,
    string Reference,
    string SerialNumber,
    string Model,
    string Market,
    DateTimeOffset RegisteredAtUtc,
    string Status,
    Guid? AgreementId,
    long Version,
    HeartbeatResponse? LastHeartbeat,
    IReadOnlyList<DeviceCommandResponse> Commands);

public sealed record HeartbeatResponse(
    DateTimeOffset RecordedAtUtc,
    decimal BatteryPercent,
    decimal OdometerKm,
    decimal Latitude,
    decimal Longitude);

public sealed record DeviceCommandResponse(
    Guid Id,
    string Type,
    string Reason,
    DateTimeOffset RequestedAtUtc,
    string Status,
    DateTimeOffset? CompletedAtUtc,
    string? Result);
