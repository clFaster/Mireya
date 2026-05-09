using Carter;
using Microsoft.AspNetCore.Mvc;
using Mireya.Application.Constants;
using Mireya.Application.Services.Asset;
using Mireya.Database.Models;

namespace Mireya.Api.Modules;

public class AssetModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/assets").RequireAuthorization(Roles.Admin);

        group.MapPost("/upload", async (
            [FromForm] UploadFilesRequest request,
            IAssetService assetService) =>
        {
            try
            {
                var result = await assetService.UploadAssetsAsync(request.Files);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        }).DisableAntiforgery();

        group.MapGet("/", async (
            IAssetService assetService,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] AssetType? type = null,
            [FromQuery] string sortBy = "name") =>
        {
            var result = await assetService.GetAssetsAsync(page, pageSize, type, sortBy);
            return Results.Ok(result);
        });

        group.MapDelete("/{id:guid}", async (Guid id, IAssetService assetService) =>
        {
            try
            {
                await assetService.DeleteAssetAsync(id);
                return Results.NoContent();
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });

        group.MapPut("/{id:guid}/metadata", async (
            Guid id,
            [FromBody] UpdateAssetMetadataRequest request,
            IAssetService assetService) =>
        {
            try
            {
                var asset = await assetService.UpdateAssetMetadataAsync(id, request);
                return Results.Ok(asset);
            }
            catch (ArgumentNullException)
            {
                return Results.BadRequest("Request body is required");
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        });

        group.MapPost("/website", async (
            [FromBody] CreateWebsiteAssetRequest request,
            IAssetService assetService) =>
        {
            try
            {
                var result = await assetService.CreateWebsiteAssetAsync(request.Url, request.Name, request.Description);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });
    }
}
