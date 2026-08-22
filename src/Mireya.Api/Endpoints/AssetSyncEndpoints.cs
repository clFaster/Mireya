using System.Security.Claims;
using Carter;
using Microsoft.AspNetCore.Mvc;
using Mireya.Application.Constants;
using Mireya.Application.Services.AssetSync;

namespace Mireya.Api.Endpoints;

public class AssetSyncEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var screenGroup = app.MapGroup("/api/assetsync").RequireAuthorization(Roles.Screen);

        screenGroup.MapPost("/status", HandleUpdateSyncStatusAsync);
        screenGroup.MapGet("/status", HandleGetSyncStatusAsync);
        screenGroup.MapGet("/campaigns", HandleGetCampaignsAsync);

        app.MapGet("/api/assetsync/{displayId:guid}/status", HandleGetDisplaySyncStatusAsync)
            .RequireAuthorization(Roles.Admin);
    }

    private static async Task<(Guid DisplayId, IResult? Error)> ResolveDisplayIdAsync(
        ClaimsPrincipal user,
        IAssetSyncService assetSyncService
    )
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return (Guid.Empty, Results.Unauthorized());

        var displayId = await assetSyncService.GetDisplayIdByUserIdAsync(userId);
        if (displayId == null)
            return (Guid.Empty, Results.NotFound("Display not found for current user"));

        return (displayId.Value, null);
    }

    private static async Task<IResult> HandleUpdateSyncStatusAsync(
        [FromBody] UpdateAssetSyncRequest request,
        ClaimsPrincipal user,
        IAssetSyncService assetSyncService,
        ILogger<AssetSyncEndpoints> logger
    )
    {
        var (displayId, error) = await ResolveDisplayIdAsync(user, assetSyncService);
        if (error != null)
            return error;

        try
        {
            var result = await assetSyncService.UpdateAssetSyncStatusAsync(displayId, request);
            return result switch
            {
                AssetSyncUpdateResult.Updated => Results.Ok(),
                AssetSyncUpdateResult.NotFound => Results.NotFound(
                    $"No sync status found for asset {request.AssetId} on this display."
                ),
                AssetSyncUpdateResult.InvalidState => Results.BadRequest(
                    $"Invalid sync state '{request.SyncState}'."
                ),
                _ => Results.Ok(),
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating sync status for display {DisplayId}", displayId);
            return Results.Problem("An error occurred while updating sync status.");
        }
    }

    private static async Task<IResult> HandleGetSyncStatusAsync(
        ClaimsPrincipal user,
        IAssetSyncService assetSyncService,
        ILogger<AssetSyncEndpoints> logger
    )
    {
        var (displayId, error) = await ResolveDisplayIdAsync(user, assetSyncService);
        if (error != null)
            return error;

        try
        {
            var statuses = await assetSyncService.GetSyncStatusForDisplayAsync(displayId);
            return Results.Ok(statuses);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving sync status for display {DisplayId}", displayId);
            return Results.Problem("An error occurred while retrieving sync status.");
        }
    }

    private static async Task<IResult> HandleGetCampaignsAsync(
        ClaimsPrincipal user,
        IAssetSyncService assetSyncService,
        ILogger<AssetSyncEndpoints> logger
    )
    {
        var (displayId, error) = await ResolveDisplayIdAsync(user, assetSyncService);
        if (error != null)
            return error;

        try
        {
            var campaigns = await assetSyncService.GetCampaignsToSyncAsync(displayId);
            return Results.Ok(campaigns);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving campaigns for display {DisplayId}", displayId);
            return Results.Problem("An error occurred while retrieving campaigns.");
        }
    }

    private static async Task<IResult> HandleGetDisplaySyncStatusAsync(
        Guid displayId,
        IAssetSyncService assetSyncService,
        ILogger<AssetSyncEndpoints> logger
    )
    {
        try
        {
            var statuses = await assetSyncService.GetSyncStatusForDisplayAsync(displayId);
            return Results.Ok(statuses);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving sync status for display {DisplayId}", displayId);
            return Results.Problem("An error occurred while retrieving sync status.");
        }
    }
}
