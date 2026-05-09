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
        // Anonymous: screen self-registration
        app.MapPost("/api/screenmanagement/register", async (
            [FromBody] RegisterScreenRequest request,
            IScreenManagementService screenManagementService) =>
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
        })
        .AllowAnonymous()
        .Produces<RegisterScreenResponse>(200);

        // Screen-authenticated: bonjour
        app.MapGet("/api/screenmanagement/bonjour", async (
            ClaimsPrincipal user,
            IScreenManagementService screenManagementService) =>
        {
            try
            {
                var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                    return Results.Unauthorized();

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
        })
        .RequireAuthorization(Roles.Screen)
        .Produces<BonjourResponse>(200);

        // Admin group
        var adminGroup = app.MapGroup("/api/screenmanagement").RequireAuthorization(Roles.Admin);

        adminGroup.MapGet("/", async (
            IScreenManagementService screenManagementService,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] ApprovalStatus? status = null,
            [FromQuery] string? sortBy = null) =>
        {
            try
            {
                var response = await screenManagementService.GetScreensAsync(page, pageSize, status, sortBy);
                return Results.Ok(response);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        adminGroup.MapGet("/{id:guid}", async (Guid id, IScreenManagementService screenManagementService) =>
        {
            try
            {
                var response = await screenManagementService.GetScreenByIdAsync(id);
                return Results.Ok(response);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = $"Screen with ID {id} not found" });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        adminGroup.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateScreenRequest request,
            IScreenManagementService screenManagementService) =>
        {
            try
            {
                var response = await screenManagementService.UpdateScreenAsync(id, request);
                return Results.Ok(response);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = $"Screen with ID {id} not found" });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        adminGroup.MapPost("/{id:guid}/approve", async (Guid id, IScreenManagementService screenManagementService) =>
        {
            try
            {
                var response = await screenManagementService.ApproveScreenAsync(id);
                return Results.Ok(response);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = $"Screen with ID {id} not found" });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        adminGroup.MapPost("/{id:guid}/reject", async (Guid id, IScreenManagementService screenManagementService) =>
        {
            try
            {
                var response = await screenManagementService.RejectScreenAsync(id);
                return Results.Ok(response);
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = $"Screen with ID {id} not found" });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        adminGroup.MapGet("/online/count", (IScreenConnectionTracker connectionTracker) =>
        {
            var count = connectionTracker.GetOnlineScreenCount();
            return Results.Ok(count);
        });
    }
}
