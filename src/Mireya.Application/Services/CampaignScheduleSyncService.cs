using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mireya.Database;

namespace Mireya.Application.Services;

/// <summary>
///     Periodically re-evaluates campaign start/end dates and pushes a fresh configuration to any
///     screen whose active campaign set has changed since the last evaluation. Without this,
///     time-based activation would only take effect when a campaign or assignment is edited.
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
                .OrderBy(a => a.Campaign.Name)
                .Select(a => a.CampaignId)
                .ToList();

            var signature = string.Join(",", activeIds);

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
