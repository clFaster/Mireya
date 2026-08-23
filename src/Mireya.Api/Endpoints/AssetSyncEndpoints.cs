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

        app.MapGet("/api/assetsync/{screenId:guid}/status", HandleGetScreenSyncStatusAsync)
            .RequireAuthorization(Roles.Admin);
    }

    private static async Task<(Guid ScreenId, IResult? Error)> ResolveScreenIdAsync(
        ClaimsPrincipal user,
        IAssetSyncService assetSyncService
    )
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return (Guid.Empty, Results.Unauthorized());

        var screenId = await assetSyncService.GetScreenIdByUserIdAsync(userId);
        if (screenId == null)
            return (Guid.Empty, Results.NotFound("Screen not found for current user"));

        return (screenId.Value, null);
    }

    private static async Task<IResult> HandleUpdateSyncStatusAsync(
        [FromBody] UpdateAssetSyncRequest request,
        ClaimsPrincipal user,
        IAssetSyncService assetSyncService,
        ILogger<AssetSyncEndpoints> logger
    )
    {
        var (screenId, error) = await ResolveScreenIdAsync(user, assetSyncService);
        if (error != null)
            return error;

        try
        {
            var result = await assetSyncService.UpdateAssetSyncStatusAsync(screenId, request);
            return result switch
            {
                AssetSyncUpdateResult.Updated => Results.Ok(),
                AssetSyncUpdateResult.NotFound => Results.NotFound(
                    $"No sync status found for asset {request.AssetId} on this screen."
                ),
                AssetSyncUpdateResult.InvalidState => Results.BadRequest(
                    $"Invalid sync state '{request.SyncState}'."
                ),
                _ => Results.Ok(),
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating sync status for screen {ScreenId}", screenId);
            return Results.Problem("An error occurred while updating sync status.");
        }
    }

    private static async Task<IResult> HandleGetSyncStatusAsync(
        ClaimsPrincipal user,
        IAssetSyncService assetSyncService,
        ILogger<AssetSyncEndpoints> logger
    )
    {
        var (screenId, error) = await ResolveScreenIdAsync(user, assetSyncService);
        if (error != null)
            return error;

        try
        {
            var statuses = await assetSyncService.GetSyncStatusForScreenAsync(screenId);
            return Results.Ok(statuses);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving sync status for screen {ScreenId}", screenId);
            return Results.Problem("An error occurred while retrieving sync status.");
        }
    }

    private static async Task<IResult> HandleGetCampaignsAsync(
        ClaimsPrincipal user,
        IAssetSyncService assetSyncService,
        ILogger<AssetSyncEndpoints> logger
    )
    {
        var (screenId, error) = await ResolveScreenIdAsync(user, assetSyncService);
        if (error != null)
            return error;

        try
        {
            var campaigns = await assetSyncService.GetCampaignsToSyncAsync(screenId);
            return Results.Ok(campaigns);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving campaigns for screen {ScreenId}", screenId);
            return Results.Problem("An error occurred while retrieving campaigns.");
        }
    }

    private static async Task<IResult> HandleGetScreenSyncStatusAsync(
        Guid screenId,
        IAssetSyncService assetSyncService,
        ILogger<AssetSyncEndpoints> logger
    )
    {
        try
        {
            var statuses = await assetSyncService.GetSyncStatusForScreenAsync(screenId);
            return Results.Ok(statuses);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving sync status for screen {ScreenId}", screenId);
            return Results.Problem("An error occurred while retrieving sync status.");
        }
    }
}
