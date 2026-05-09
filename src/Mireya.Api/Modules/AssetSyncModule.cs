using System.Security.Claims;
using Carter;
using Microsoft.AspNetCore.Mvc;
using Mireya.Application.Constants;
using Mireya.Application.Services.AssetSync;

namespace Mireya.Api.Modules;

public class AssetSyncModule : ICarterModule
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

    private static async Task<IResult> HandleUpdateSyncStatusAsync(
        [FromBody] UpdateAssetSyncRequest request,
        ClaimsPrincipal user,
        IAssetSyncService assetSyncService)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        var displayId = await assetSyncService.GetDisplayIdByUserIdAsync(userId);
        if (displayId == null)
            return Results.NotFound("Display not found for current user");

        try
        {
            await assetSyncService.UpdateAssetSyncStatusAsync(displayId.Value, request);
            return Results.Ok();
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }

    private static async Task<IResult> HandleGetSyncStatusAsync(
        ClaimsPrincipal user,
        IAssetSyncService assetSyncService)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        var displayId = await assetSyncService.GetDisplayIdByUserIdAsync(userId);
        if (displayId == null)
            return Results.NotFound("Display not found for current user");

        try
        {
            var statuses = await assetSyncService.GetSyncStatusForDisplayAsync(displayId.Value);
            return Results.Ok(statuses);
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }

    private static async Task<IResult> HandleGetCampaignsAsync(
        ClaimsPrincipal user,
        IAssetSyncService assetSyncService)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        var displayId = await assetSyncService.GetDisplayIdByUserIdAsync(userId);
        if (displayId == null)
            return Results.NotFound("Display not found for current user");

        try
        {
            var campaigns = await assetSyncService.GetCampaignsToSyncAsync(displayId.Value);
            return Results.Ok(campaigns);
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }

    private static async Task<IResult> HandleGetDisplaySyncStatusAsync(
        Guid displayId,
        IAssetSyncService assetSyncService)
    {
        try
        {
            var statuses = await assetSyncService.GetSyncStatusForDisplayAsync(displayId);
            return Results.Ok(statuses);
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }
}
