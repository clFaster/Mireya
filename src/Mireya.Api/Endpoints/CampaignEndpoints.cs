using Carter;
using Microsoft.AspNetCore.Mvc;
using Mireya.Application.Constants;
using Mireya.Application.Services.Campaign;

namespace Mireya.Api.Endpoints;

public class CampaignEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/campaign").RequireAuthorization(Roles.Admin);

        group.MapGet("/", HandleGetCampaignsAsync);
        group
            .MapGet("/fallback", HandleGetGlobalFallbackAsync)
            .Produces<CampaignAssignmentDetail>()
            .Produces(StatusCodes.Status204NoContent);
        group
            .MapPut("/fallback", HandleSetGlobalFallbackAsync)
            .Produces<CampaignAssignmentDetail>()
            .Produces(StatusCodes.Status400BadRequest);
        group.MapDelete("/fallback", HandleClearGlobalFallbackAsync);
        group.MapGet("/{id:guid}", HandleGetCampaignAsync);
        group.MapPost("/", HandleCreateCampaignAsync);
        group.MapPut("/{id:guid}", HandleUpdateCampaignAsync);
        group.MapDelete("/{id:guid}", HandleDeleteCampaignAsync);
    }

    private static async Task<IResult> HandleGetGlobalFallbackAsync(
        ICampaignService campaignService
    )
    {
        var assignment = await campaignService.GetGlobalFallbackAsync();
        return assignment == null ? Results.NoContent() : Results.Ok(assignment);
    }

    private static async Task<IResult> HandleSetGlobalFallbackAsync(
        [FromBody] CampaignAssignmentRequest request,
        ICampaignService campaignService
    )
    {
        try
        {
            return Results.Ok(await campaignService.SetGlobalFallbackAsync(request));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> HandleClearGlobalFallbackAsync(
        ICampaignService campaignService
    )
    {
        await campaignService.ClearGlobalFallbackAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> HandleGetCampaignsAsync(
        ICampaignService campaignService,
        ILogger<CampaignEndpoints> logger,
        [FromQuery] Guid? screenId = null
    )
    {
        try
        {
            var campaigns = await campaignService.GetCampaignsAsync(screenId);
            return Results.Ok(campaigns);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving campaigns");
            return Results.Problem("An error occurred while retrieving campaigns.");
        }
    }

    private static async Task<IResult> HandleGetCampaignAsync(
        Guid id,
        ICampaignService campaignService,
        ILogger<CampaignEndpoints> logger
    )
    {
        try
        {
            var campaign = await campaignService.GetCampaignAsync(id);
            return Results.Ok(campaign);
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound($"Campaign with ID {id} not found");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving campaign {CampaignId}", id);
            return Results.Problem("An error occurred while retrieving the campaign.");
        }
    }

    private static async Task<IResult> HandleCreateCampaignAsync(
        [FromBody] CreateCampaignRequest request,
        ICampaignService campaignService,
        ILogger<CampaignEndpoints> logger
    )
    {
        try
        {
            var campaign = await campaignService.CreateCampaignAsync(request);
            return Results.Created($"/api/campaign/{campaign.Id}", campaign);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating campaign");
            return Results.Problem("An error occurred while creating the campaign.");
        }
    }

    private static async Task<IResult> HandleUpdateCampaignAsync(
        Guid id,
        [FromBody] UpdateCampaignRequest request,
        ICampaignService campaignService,
        ILogger<CampaignEndpoints> logger
    )
    {
        try
        {
            var campaign = await campaignService.UpdateCampaignAsync(id, request);
            return Results.Ok(campaign);
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound($"Campaign with ID {id} not found");
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating campaign {CampaignId}", id);
            return Results.Problem("An error occurred while updating the campaign.");
        }
    }

    private static async Task<IResult> HandleDeleteCampaignAsync(
        Guid id,
        ICampaignService campaignService,
        ILogger<CampaignEndpoints> logger
    )
    {
        try
        {
            await campaignService.DeleteCampaignAsync(id);
            return Results.NoContent();
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound($"Campaign with ID {id} not found");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting campaign {CampaignId}", id);
            return Results.Problem("An error occurred while deleting the campaign.");
        }
    }
}
