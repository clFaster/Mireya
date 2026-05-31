using Carter;
using Microsoft.AspNetCore.Mvc;
using Mireya.Application.Constants;
using Mireya.Application.Services.Audit;

namespace Mireya.Api.Endpoints;

public class AuditEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/audit").RequireAuthorization(Roles.Admin);

        group.MapGet("/", HandleGetAuditLogAsync);
    }

    private static async Task<IResult> HandleGetAuditLogAsync(
        IAuditService auditService,
        ILogger<AuditEndpoints> logger,
        [FromQuery] int take = 200)
    {
        try
        {
            var entries = await auditService.GetRecentAsync(take);
            return Results.Ok(entries);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving audit log");
            return Results.Problem("An error occurred while retrieving the audit log.");
        }
    }
}
