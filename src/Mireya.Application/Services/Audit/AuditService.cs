using Microsoft.EntityFrameworkCore;
using Mireya.Database;
using Mireya.Database.Models;

namespace Mireya.Application.Services.Audit;

/// <summary>
///     A single audit-log entry as exposed to the admin UI / API.
/// </summary>
public record AuditLogEntry(
    Guid Id,
    DateTime Timestamp,
    string? ActorName,
    string Action,
    string EntityType,
    string? EntityId,
    string? Summary
);

public interface IAuditService
{
    /// <summary>
    ///     Records a mutating admin action. Never throws: audit failures are logged and swallowed
    ///     so they cannot break the underlying operation.
    /// </summary>
    Task LogAsync(
        string action,
        string entityType,
        string? entityId = null,
        string? summary = null
    );

    /// <summary>
    ///     Returns the most recent audit-log entries, newest first.
    /// </summary>
    Task<List<AuditLogEntry>> GetRecentAsync(int take = 200);
}

public class AuditService(
    MireyaDbContext db,
    ICurrentUserContext userContext,
    ILogger<AuditService> logger
) : IAuditService
{
    public async Task LogAsync(
        string action,
        string entityType,
        string? entityId = null,
        string? summary = null
    )
    {
        try
        {
            var (userId, userName) = await userContext.GetCurrentUserAsync();
            db.AuditLogs.Add(
                new AuditLog
                {
                    Timestamp = DateTime.UtcNow,
                    ActorUserId = userId,
                    ActorName = userName,
                    Action = action,
                    EntityType = entityType,
                    EntityId = entityId,
                    Summary = summary,
                }
            );
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Auditing must never break the action it records.
            logger.LogError(
                ex,
                "Failed to write audit log entry for {Action} {EntityType} {EntityId}",
                action,
                entityType,
                entityId
            );
        }
    }

    public async Task<List<AuditLogEntry>> GetRecentAsync(int take = 200)
    {
        take = Math.Clamp(take, 1, 1000);
        return await db
            .AuditLogs.OrderByDescending(a => a.Timestamp)
            .Take(take)
            .Select(a => new AuditLogEntry(
                a.Id,
                a.Timestamp,
                a.ActorName,
                a.Action,
                a.EntityType,
                a.EntityId,
                a.Summary
            ))
            .ToListAsync();
    }
}
