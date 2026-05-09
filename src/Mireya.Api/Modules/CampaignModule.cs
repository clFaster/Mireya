using Carter;
using Microsoft.AspNetCore.Mvc;
using Mireya.Application.Constants;
using Mireya.Application.Services.Campaign;

namespace Mireya.Api.Modules;

public class CampaignModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/campaign").RequireAuthorization(Roles.Admin);

        group.MapGet("/", HandleGetCampaignsAsync);
        group.MapGet("/{id:guid}", HandleGetCampaignAsync);
        group.MapPost("/", HandleCreateCampaignAsync);
        group.MapPut("/{id:guid}", HandleUpdateCampaignAsync);
        group.MapDelete("/{id:guid}", HandleDeleteCampaignAsync);
    }

    private static async Task<IResult> HandleGetCampaignsAsync(
        ICampaignService campaignService,
        [FromQuery] Guid? displayId = null)
    {
        try
        {
            var campaigns = await campaignService.GetCampaignsAsync(displayId);
            return Results.Ok(campaigns);
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }

    private static async Task<IResult> HandleGetCampaignAsync(
        Guid id,
        ICampaignService campaignService)
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
            return Results.Problem(ex.Message);
        }
    }

    private static async Task<IResult> HandleCreateCampaignAsync(
        [FromBody] CreateCampaignRequest request,
        ICampaignService campaignService)
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
            return Results.Problem(ex.Message);
        }
    }

    private static async Task<IResult> HandleUpdateCampaignAsync(
        Guid id,
        [FromBody] UpdateCampaignRequest request,
        ICampaignService campaignService)
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
            return Results.Problem(ex.Message);
        }
    }

    private static async Task<IResult> HandleDeleteCampaignAsync(
        Guid id,
        ICampaignService campaignService)
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
            return Results.Problem(ex.Message);
        }
    }
}
