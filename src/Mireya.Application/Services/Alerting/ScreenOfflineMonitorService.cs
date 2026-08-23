using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Mireya.Database;
using Mireya.Database.Models;

namespace Mireya.Application.Services.Alerting;

/// <summary>
///     Periodically checks approved screens and raises a webhook alert when one has been offline
///     longer than the configured threshold, and a recovery alert when it comes back. State is
///     tracked via <see cref="Screen.OfflineAlertedAt" /> so each outage alerts exactly once and
///     survives application restarts.
/// </summary>
public class ScreenOfflineMonitorService(
    IServiceScopeFactory scopeFactory,
    IOptions<AlertingOptions> options,
    ILogger<ScreenOfflineMonitorService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.OfflineWebhookUrl))
        {
            logger.LogInformation("Screen offline monitoring is disabled.");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(15, settings.PollIntervalSeconds));
        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<MireyaDbContext>();
                var alerts = scope.ServiceProvider.GetRequiredService<IScreenAlertService>();
                await EvaluateOnceAsync(
                    db,
                    alerts,
                    settings.OfflineThresholdMinutes,
                    DateTime.UtcNow,
                    stoppingToken
                );
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Screen offline monitoring cycle failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>
    ///     Evaluates all approved screens once: raises offline alerts for screens that have been
    ///     offline beyond the threshold and have not yet been alerted, and recovery alerts for
    ///     screens that are back online but were previously alerted. Returns the number of alerts sent.
    /// </summary>
    public static async Task<int> EvaluateOnceAsync(
        MireyaDbContext db,
        IScreenAlertService alerts,
        int thresholdMinutes,
        DateTime utcNow,
        CancellationToken cancellationToken = default
    )
    {
        var threshold = utcNow.AddMinutes(-Math.Max(1, thresholdMinutes));
        var sent = 0;

        var approved = await db
            .Screens.Where(d => d.ApprovalStatus == ApprovalStatus.Approved)
            .ToListAsync(cancellationToken);

        foreach (var screen in approved)
        {
            var offlineLongEnough =
                !screen.IsActive
                && screen.LastSeenAt.HasValue
                && screen.LastSeenAt.Value <= threshold;

            if (offlineLongEnough && screen.OfflineAlertedAt is null)
            {
                await alerts.SendAsync(ScreenAlertKind.Offline, screen, cancellationToken);
                screen.OfflineAlertedAt = utcNow;
                sent++;
            }
            else if (screen.IsActive && screen.OfflineAlertedAt is not null)
            {
                await alerts.SendAsync(ScreenAlertKind.Online, screen, cancellationToken);
                screen.OfflineAlertedAt = null;
                sent++;
            }
        }

        if (sent > 0)
            await db.SaveChangesAsync(cancellationToken);

        return sent;
    }
}
