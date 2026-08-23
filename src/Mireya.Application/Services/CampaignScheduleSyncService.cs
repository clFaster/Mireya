using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mireya.Database;

namespace Mireya.Application.Services;

/// <summary>
///     Periodically re-evaluates campaign schedules (start/end dates and weekday/daily-time
///     recurrence) and pushes a fresh configuration to any screen whose active campaign set has
///     changed since the last evaluation. Without this, time-based activation would only take
///     effect when a campaign or assignment is edited.
/// </summary>
public class CampaignScheduleSyncService(
    IServiceScopeFactory scopeFactory,
    ILogger<CampaignScheduleSyncService> logger
) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);
    private readonly Dictionary<Guid, string> _lastSignatures = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await EvaluateSchedulesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Campaign schedule evaluation failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task EvaluateSchedulesAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MireyaDbContext>();
        var syncService = scope.ServiceProvider.GetRequiredService<IScreenSynchronizationService>();

        var utcNow = DateTime.UtcNow;

        var fallbackAssignment = await db
            .CampaignAssignments.Where(a =>
                a.TargetKind == Database.Models.CampaignAssignmentTargetKind.GlobalFallback
            )
            .FirstOrDefaultAsync(cancellationToken);

        var fallbackActiveCampaignId =
            fallbackAssignment is not null && fallbackAssignment.IsActiveAt(utcNow)
                ? fallbackAssignment.CampaignId
                : (Guid?)null;

        var screens = await db
            .Screens.Where(d => d.UserId != null)
            .Include(d => d.CampaignAssignments)
                .ThenInclude(ca => ca.Campaign)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        foreach (var screen in screens)
        {
            var activeIds = screen
                .CampaignAssignments.Where(a => a.IsActiveAt(utcNow))
                .OrderByDescending(a => a.Priority)
                .ThenBy(a => a.Campaign.Name)
                .Select(a => a.CampaignId)
                .ToList();

            var signature =
                activeIds.Count > 0
                    ? string.Join(",", activeIds)
                    : $"fallback:{fallbackActiveCampaignId?.ToString() ?? "none"}";

            if (_lastSignatures.TryGetValue(screen.Id, out var previous) && previous == signature)
                continue;

            // Skip the very first observation (initial sync happens on edits / client connect)
            // to avoid a burst of redundant syncs on startup.
            var isFirstObservation = !_lastSignatures.ContainsKey(screen.Id);
            _lastSignatures[screen.Id] = signature;
            if (isFirstObservation)
                continue;

            logger.LogInformation(
                "Schedule change detected for screen {ScreenId}; re-syncing",
                screen.Id
            );
            await syncService.SyncScreenAsync(screen.Id);
        }
    }
}
