using System.Security.Claims;
using Carter;
using Microsoft.AspNetCore.Mvc;
using Mireya.Application.Constants;
using Mireya.Application.Services;
using Mireya.Application.Services.ScreenManagement;
using Mireya.Database.Models;

namespace Mireya.Api.Modules;

public class ScreenManagementModule : ICarterModule
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
        IScreenManagementService screenManagementService)
    {
        try
        {
            var response = await screenManagementService.RegisterScreenAsync(request);
            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> HandleBonjourAsync(
        ClaimsPrincipal user,
        IScreenManagementService screenManagementService)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        try
        {
            var response = await screenManagementService.GetBonjourAsync(userId);
            return Results.Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private record GetScreensQuery(
        [property: FromQuery] int Page = 1,
        [property: FromQuery] int PageSize = 10,
        [property: FromQuery] ApprovalStatus? Status = null,
        [property: FromQuery] string? SortBy = null);

    private static async Task<IResult> HandleGetScreensAsync(
        IScreenManagementService screenManagementService,
        [AsParameters] GetScreensQuery query)
    {
        try
        {
            var response = await screenManagementService.GetScreensAsync(query.Page, query.PageSize, query.Status, query.SortBy);
            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> ExecuteScreenOperationAsync(Func<Task<object>> operation, Guid id)
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
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static Task<IResult> HandleGetScreenByIdAsync(
        Guid id,
        IScreenManagementService screenManagementService) =>
        ExecuteScreenOperationAsync(async () => await screenManagementService.GetScreenByIdAsync(id), id);

    private static Task<IResult> HandleUpdateScreenAsync(
        Guid id,
        [FromBody] UpdateScreenRequest request,
        IScreenManagementService screenManagementService) =>
        ExecuteScreenOperationAsync(async () => await screenManagementService.UpdateScreenAsync(id, request), id);

    private static Task<IResult> HandleApproveScreenAsync(
        Guid id,
        IScreenManagementService screenManagementService) =>
        ExecuteScreenOperationAsync(async () => await screenManagementService.ApproveScreenAsync(id), id);

    private static Task<IResult> HandleRejectScreenAsync(
        Guid id,
        IScreenManagementService screenManagementService) =>
        ExecuteScreenOperationAsync(async () => await screenManagementService.RejectScreenAsync(id), id);

    private static IResult HandleGetOnlineCountAsync(IScreenConnectionTracker connectionTracker)
    {
        var count = connectionTracker.GetOnlineScreenCount();
        return Results.Ok(count);
    }
}
