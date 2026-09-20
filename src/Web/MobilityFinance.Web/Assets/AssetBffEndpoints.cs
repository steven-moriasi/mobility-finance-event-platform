using Microsoft.AspNetCore.Mvc;
using MobilityFinance.Web.Security;

namespace MobilityFinance.Web.Assets;

public static class AssetBffEndpoints
{
    public static IEndpointRouteBuilder MapAssetBff(
        this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder operatorGroup = endpoints
            .MapGroup("/bff/assets")
            .RequireAuthorization(AuthorizationPolicies.OperatorWork)
            .ExcludeFromDescription();

        operatorGroup.MapPost(
            "/",
            async (
                [FromForm] RegisterAssetForm form,
                AssetClient client,
                CancellationToken cancellationToken) =>
            {
                AssetResult<AssetDto> result = await client.RegisterAssetAsync(
                    new RegisterAssetCommand(
                        form.Reference,
                        form.SerialNumber,
                        form.Model,
                        "KE"),
                    cancellationToken);

                return result.IsSuccess
                    ? Results.LocalRedirect(
                        $"/assets?registered={result.Value!.Id}")
                    : RedirectWithError(result.Error!);
            });

        operatorGroup.MapPost(
            "/{assetId:guid}/assign",
            async (
                Guid assetId,
                [FromForm] AssignAssetForm form,
                AssetClient client,
                CancellationToken cancellationToken) =>
            {
                AssetResult<AssetAssignmentDto> result =
                    await client.AssignAssetAsync(
                        assetId,
                        form.AgreementId,
                        cancellationToken);

                return result.IsSuccess
                    ? Results.LocalRedirect(
                        $"/assets?assigned={result.Value!.Asset.Id}&duplicate={result.Value.IsDuplicate}")
                    : RedirectWithError(result.Error!);
            });

        operatorGroup.MapPost(
            "/{assetId:guid}/simulation/tick",
            async (
                Guid assetId,
                AssetClient client,
                CancellationToken cancellationToken) =>
            {
                AssetResult<AssetDto> result =
                    await client.SimulateDeviceAsync(
                        assetId,
                        cancellationToken);

                return result.IsSuccess
                    ? Results.LocalRedirect(
                        $"/assets?simulated={result.Value!.Id}")
                    : RedirectWithError(result.Error!);
            });

        endpoints
            .MapPost(
                "/bff/assets/{assetId:guid}/commands",
                async (
                    Guid assetId,
                    [FromForm] QueueDeviceCommandForm form,
                    AssetClient client,
                    CancellationToken cancellationToken) =>
                {
                    AssetResult<DeviceCommandDto> result =
                        await client.QueueCommandAsync(
                            assetId,
                            new QueueDeviceCommand(form.Type, form.Reason),
                            cancellationToken);

                    return result.IsSuccess
                        ? Results.LocalRedirect(
                            $"/assets?command={result.Value!.Id}")
                        : RedirectWithError(result.Error!);
                })
            .RequireAuthorization(
                AuthorizationPolicies.PlatformAdministration)
            .ExcludeFromDescription();

        return endpoints;
    }

    private static IResult RedirectWithError(string error)
    {
        return Results.LocalRedirect(
            $"/assets?error={Uri.EscapeDataString(error)}");
    }
}

public sealed record RegisterAssetForm(
    string Reference,
    string SerialNumber,
    string Model);

public sealed record AssignAssetForm(Guid AgreementId);

public sealed record QueueDeviceCommandForm(int Type, string Reason);
