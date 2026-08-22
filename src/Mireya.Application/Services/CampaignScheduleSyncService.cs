using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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

        var defaultCampaign = await db
            .Campaigns.Where(c => c.IsDefault)
            .Select(c => new
            {
                c.Id,
                c.IsEnabled,
                c.StartDateUtc,
                c.EndDateUtc,
                c.RecurrenceDaysMask,
                c.DailyStartTime,
                c.DailyEndTime,
                c.RecurrenceTimeZoneId,
            })
            .FirstOrDefaultAsync(cancellationToken);

        var defaultActiveId =
            defaultCampaign is not null
            && new Database.Models.Campaign
            {
                IsEnabled = defaultCampaign.IsEnabled,
                StartDateUtc = defaultCampaign.StartDateUtc,
                EndDateUtc = defaultCampaign.EndDateUtc,
                RecurrenceDaysMask = defaultCampaign.RecurrenceDaysMask,
                DailyStartTime = defaultCampaign.DailyStartTime,
                DailyEndTime = defaultCampaign.DailyEndTime,
                RecurrenceTimeZoneId = defaultCampaign.RecurrenceTimeZoneId,
            }.IsActiveAt(utcNow)
                ? defaultCampaign.Id
                : (Guid?)null;

        var displays = await db
            .Displays.Where(d => d.UserId != null)
            .Include(d => d.CampaignAssignments)
                .ThenInclude(ca => ca.Campaign)
            .Include(d => d.Zone)
                .ThenInclude(z => z!.ZoneCampaigns)
                    .ThenInclude(zc => zc.Campaign)
            .ToListAsync(cancellationToken);

        foreach (var display in displays)
        {
            var directCampaigns = display.CampaignAssignments.Select(ca => ca.Campaign);
            var zoneCampaigns =
                display.Zone?.ZoneCampaigns.Select(zc => zc.Campaign)
                ?? Enumerable.Empty<Database.Models.Campaign>();

            var activeIds = directCampaigns
                .Concat(zoneCampaigns)
                .GroupBy(c => c.Id)
                .Select(g => g.First())
                .Where(c => c.IsActiveAt(utcNow))
                .OrderByDescending(c => c.Priority)
                .ThenBy(c => c.Name)
                .Select(c => c.Id)
                .ToList();

            var signature =
                activeIds.Count > 0
                    ? string.Join(",", activeIds)
                    : $"default:{defaultActiveId?.ToString() ?? "none"}";

            if (_lastSignatures.TryGetValue(display.Id, out var previous) && previous == signature)
                continue;

            // Skip the very first observation (initial sync happens on edits / client connect)
            // to avoid a burst of redundant syncs on startup.
            var isFirstObservation = !_lastSignatures.ContainsKey(display.Id);
            _lastSignatures[display.Id] = signature;
            if (isFirstObservation)
                continue;

            logger.LogInformation(
                "Schedule change detected for screen {DisplayId}; re-syncing",
                display.Id
            );
            await syncService.SyncScreenAsync(display.Id);
        }
    }
}
