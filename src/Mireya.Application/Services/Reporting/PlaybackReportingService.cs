using Microsoft.EntityFrameworkCore;
using Mireya.Database;
using Mireya.Database.Models;

namespace Mireya.Application.Services.Reporting;

/// <summary>
///     A single proof-of-play event as exposed to the admin UI.
/// </summary>
public record PlaybackEventEntry(
    Guid Id,
    DateTime PlayedAtUtc,
    Guid ScreenId,
    string ScreenName,
    Guid? AssetId,
    string? AssetName
);

/// <summary>
///     Aggregated play counts grouped by asset.
/// </summary>
public record AssetPlayCount(Guid? AssetId, string AssetName, int Plays);

/// <summary>
///     Aggregated play counts grouped by screen.
/// </summary>
public record ScreenPlayCount(Guid ScreenId, string ScreenName, int Plays);

/// <summary>
///     A proof-of-play report over a time window.
/// </summary>
public record PlaybackReport(
    DateTime FromUtc,
    DateTime ToUtc,
    int TotalPlays,
    int DistinctAssets,
    int DistinctScreens,
    IReadOnlyList<AssetPlayCount> ByAsset,
    IReadOnlyList<ScreenPlayCount> ByScreen
);

public interface IPlaybackReportingService
{
    /// <summary>
    ///     Persists a proof-of-play event for the given screen user. Never throws: reporting
    ///     failures are logged and swallowed so they cannot disrupt the SignalR hub. No-ops when
    ///     the user cannot be resolved to a known screen or no asset is supplied.
    /// </summary>
    Task RecordAsync(string userId, Guid? assetId, string? assetName);

    /// <summary>
    ///     Builds an aggregated proof-of-play report for the given UTC window.
    /// </summary>
    Task<PlaybackReport> GetReportAsync(DateTime fromUtc, DateTime toUtc);

    /// <summary>
    ///     Returns the most recent proof-of-play events, newest first.
    /// </summary>
    Task<List<PlaybackEventEntry>> GetRecentAsync(int take = 200);
}

public class PlaybackReportingService(MireyaDbContext db, ILogger<PlaybackReportingService> logger)
    : IPlaybackReportingService
{
    public async Task RecordAsync(string userId, Guid? assetId, string? assetName)
    {
        if (string.IsNullOrEmpty(userId) || assetId is null)
            return;

        try
        {
            var screen = await db
                .Screens.Where(d => d.UserId == userId)
                .Select(d => new { d.Id, d.Name })
                .FirstOrDefaultAsync();

            if (screen is null)
                return;

            db.PlaybackEvents.Add(
                new PlaybackEvent
                {
                    ScreenId = screen.Id,
                    ScreenName = screen.Name,
                    AssetId = assetId,
                    AssetName = assetName,
                    PlayedAtUtc = DateTime.UtcNow,
                }
            );
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Proof-of-play must never break live playback reporting.
            logger.LogError(
                ex,
                "Failed to record playback event for user {UserId} asset {AssetId}",
                userId,
                assetId
            );
        }
    }

    public async Task<PlaybackReport> GetReportAsync(DateTime fromUtc, DateTime toUtc)
    {
        var events = db.PlaybackEvents.Where(e =>
            e.PlayedAtUtc >= fromUtc && e.PlayedAtUtc <= toUtc
        );

        var total = await events.CountAsync();

        var byAssetRaw = await events
            .GroupBy(e => new { e.AssetId, e.AssetName })
            .Select(g => new
            {
                g.Key.AssetId,
                g.Key.AssetName,
                Plays = g.Count(),
            })
            .ToListAsync();

        var byAsset = byAssetRaw
            .Select(a => new AssetPlayCount(a.AssetId, a.AssetName ?? "(unknown)", a.Plays))
            .OrderByDescending(a => a.Plays)
            .ToList();

        var byScreenRaw = await events
            .GroupBy(e => new { e.ScreenId, e.ScreenName })
            .Select(g => new
            {
                g.Key.ScreenId,
                g.Key.ScreenName,
                Plays = g.Count(),
            })
            .ToListAsync();

        var byScreen = byScreenRaw
            .Select(s => new ScreenPlayCount(s.ScreenId, s.ScreenName, s.Plays))
            .OrderByDescending(s => s.Plays)
            .ToList();

        return new PlaybackReport(
            fromUtc,
            toUtc,
            total,
            byAsset.Count,
            byScreen.Count,
            byAsset,
            byScreen
        );
    }

    public async Task<List<PlaybackEventEntry>> GetRecentAsync(int take = 200)
    {
        take = Math.Clamp(take, 1, 1000);
        return await db
            .PlaybackEvents.OrderByDescending(e => e.PlayedAtUtc)
            .Take(take)
            .Select(e => new PlaybackEventEntry(
                e.Id,
                e.PlayedAtUtc,
                e.ScreenId,
                e.ScreenName,
                e.AssetId,
                e.AssetName
            ))
            .ToListAsync();
    }
}
