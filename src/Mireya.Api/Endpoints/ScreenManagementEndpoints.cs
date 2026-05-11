using System.Security.Claims;
using Carter;
using Microsoft.AspNetCore.Mvc;
using Mireya.Application.Constants;
using Mireya.Application.Services;
using Mireya.Application.Services.ScreenManagement;
using Mireya.Database.Models;

namespace Mireya.Api.Endpoints;

public class ScreenManagementEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/screenmanagement/register", HandleRegisterAsync)
            .AllowAnonymous()
            .Produces<RegisterScreenResponse>(200);

        app.MapGet("/api/screenmanagement/bonjour", HandleBonjourAsync)
            .RequireAuthorization(Roles.Screen)
            .Produces<BonjourResponse>(200);

        var adminGroup = app.MapGroup("/api/screenmanagement").RequireAuthorization(Roles.Admin);

        adminGroup.MapGet("/", HandleGetScreensAsync);
        adminGroup.MapGet("/{id:guid}", HandleGetScreenByIdAsync);
        adminGroup.MapPut("/{id:guid}", HandleUpdateScreenAsync);
        adminGroup.MapPost("/{id:guid}/approve", HandleApproveScreenAsync);
        adminGroup.MapPost("/{id:guid}/reject", HandleRejectScreenAsync);
        adminGroup.MapGet("/online/count", HandleGetOnlineCountAsync);
    }

    private static async Task<IResult> HandleRegisterAsync(
        [FromBody] RegisterScreenRequest request,
        IScreenManagementService screenManagementService,
        ILogger<ScreenManagementEndpoints> logger)
    {
        try
        {
            var response = await screenManagementService.RegisterScreenAsync(request);
            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error registering screen");
            return Results.BadRequest(new { error = "Failed to register screen. Please try again." });
        }
    }

    private static async Task<IResult> HandleBonjourAsync(
        ClaimsPrincipal user,
        IScreenManagementService screenManagementService,
        ILogger<ScreenManagementEndpoints> logger)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            var response = await screenManagementService.GetBonjourAsync(userId);
            return Results.Ok(response);
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound(new { error = "Screen not found for current user" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during bonjour for user {UserId}", userId);
            return Results.BadRequest(new { error = "An error occurred during check-in." });
        }
    }

    private record GetScreensQuery(
        [property: FromQuery] int Page = 1,
        [property: FromQuery] int PageSize = 10,
        [property: FromQuery] ApprovalStatus? Status = null,
        [property: FromQuery] string? SortBy = null);

    private static async Task<IResult> HandleGetScreensAsync(
        IScreenManagementService screenManagementService,
        ILogger<ScreenManagementEndpoints> logger,
        [AsParameters] GetScreensQuery query)
    {
        try
        {
            var response = await screenManagementService.GetScreensAsync(query.Page, query.PageSize, query.Status, query.SortBy);
            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving screens");
            return Results.BadRequest(new { error = "An error occurred while retrieving screens." });
        }
    }

    private static async Task<IResult> ExecuteScreenOperationAsync(
        Func<Task<object>> operation, Guid id, ILogger logger)
    {
        try
        {
            return Results.Ok(await operation());
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound(new { error = $"Screen with ID {id} not found" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error performing operation on screen {ScreenId}", id);
            return Results.BadRequest(new { error = "An error occurred while processing the request." });
        }
    }

    private static Task<IResult> HandleGetScreenByIdAsync(
        Guid id,
        IScreenManagementService screenManagementService,
        ILogger<ScreenManagementEndpoints> logger) =>
        ExecuteScreenOperationAsync(async () => await screenManagementService.GetScreenByIdAsync(id), id, logger);

    private static Task<IResult> HandleUpdateScreenAsync(
        Guid id,
        [FromBody] UpdateScreenRequest request,
        IScreenManagementService screenManagementService,
        ILogger<ScreenManagementEndpoints> logger) =>
        ExecuteScreenOperationAsync(async () => await screenManagementService.UpdateScreenAsync(id, request), id, logger);

    private static Task<IResult> HandleApproveScreenAsync(
        Guid id,
        IScreenManagementService screenManagementService,
        ILogger<ScreenManagementEndpoints> logger) =>
        ExecuteScreenOperationAsync(async () => await screenManagementService.ApproveScreenAsync(id), id, logger);

    private static Task<IResult> HandleRejectScreenAsync(
        Guid id,
        IScreenManagementService screenManagementService,
        ILogger<ScreenManagementEndpoints> logger) =>
        ExecuteScreenOperationAsync(async () => await screenManagementService.RejectScreenAsync(id), id, logger);

    private static IResult HandleGetOnlineCountAsync(IScreenConnectionTracker connectionTracker)
    {
        var count = connectionTracker.GetOnlineScreenCount();
        return Results.Ok(count);
    }
}
