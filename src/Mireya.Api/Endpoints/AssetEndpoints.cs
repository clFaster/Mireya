using Carter;
using Microsoft.AspNetCore.Mvc;
using Mireya.Application.Constants;
using Mireya.Application.Services.Asset;
using Mireya.Database.Models;

namespace Mireya.Api.Endpoints;

public class AssetEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/assets").RequireAuthorization(Roles.Admin);

        group.MapPost("/upload", HandleUploadAsync).DisableAntiforgery();
        group.MapGet("/", HandleGetAssetsAsync);
        group.MapDelete("/{id:guid}", HandleDeleteAsync);
        group.MapPut("/{id:guid}/metadata", HandleUpdateMetadataAsync);
        group.MapPost("/website", HandleCreateWebsiteAssetAsync);
    }

    private static async Task<IResult> HandleUploadAsync(
        [FromForm] UploadFilesRequest request,
        IAssetService assetService)
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
    }

    private record GetAssetsQuery(
        [property: FromQuery] int Page = 1,
        [property: FromQuery] int PageSize = 10,
        [property: FromQuery] AssetType? Type = null,
        [property: FromQuery] string SortBy = "name");

    private static async Task<IResult> HandleGetAssetsAsync(
        IAssetService assetService,
        [AsParameters] GetAssetsQuery query)
    {
        var result = await assetService.GetAssetsAsync(new AssetFilter(query.Page, query.PageSize, query.Type, query.SortBy));
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleDeleteAsync(Guid id, IAssetService assetService)
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
    }

    private static async Task<IResult> HandleUpdateMetadataAsync(
        Guid id,
        [FromBody] UpdateAssetMetadataRequest request,
        IAssetService assetService)
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
    }

    private static async Task<IResult> HandleCreateWebsiteAssetAsync(
        [FromBody] CreateWebsiteAssetRequest request,
        IAssetService assetService)
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
    }
}
