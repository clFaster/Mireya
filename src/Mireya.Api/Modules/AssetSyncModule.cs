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

        screenGroup.MapPost("/status", async (
            [FromBody] UpdateAssetSyncRequest request,
            ClaimsPrincipal user,
            IAssetSyncService assetSyncService) =>
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
        });

        screenGroup.MapGet("/status", async (
            ClaimsPrincipal user,
            IAssetSyncService assetSyncService) =>
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
        });

        screenGroup.MapGet("/campaigns", async (
            ClaimsPrincipal user,
            IAssetSyncService assetSyncService) =>
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
        });

        // Admin endpoint - separate group
        app.MapGet("/api/assetsync/{displayId:guid}/status", async (
            Guid displayId,
            IAssetSyncService assetSyncService) =>
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
        }).RequireAuthorization(Roles.Admin);
    }
}
