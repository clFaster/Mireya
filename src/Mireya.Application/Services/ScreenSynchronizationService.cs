using Microsoft.EntityFrameworkCore;
using Mireya.Application.Constants;
using Mireya.Application.Hubs;
using Mireya.Application.Services.AssetSync;
using Mireya.Application.Services.Campaign;
using Mireya.Application.Services.ScreenManagement;
using Mireya.Database;
using Mireya.Database.Models;

namespace Mireya.Application.Services;

public interface IScreenSynchronizationService
{
    Task SyncScreenAsync(Guid screenId);
    Task SyncScreensAsync(IEnumerable<Guid> screenIds);
    Task<Guid?> GetScreenIdByUserIdAsync(string userId);
    Task<bool> SendCommandAsync(Guid screenId, string command);
}

public class ScreenSynchronizationService(
    MireyaDbContext db,
    IScreenHubContext hubContext,
    IAssetSyncService assetSyncService,
    ILogger<ScreenSynchronizationService> logger
) : IScreenSynchronizationService
{
    public async Task SyncScreensAsync(IEnumerable<Guid> screenIds)
    {
        // Sequential execution required: DbContext is not thread-safe
        foreach (var screenId in screenIds.Distinct())
            await SyncScreenAsync(screenId);
    }

    public async Task SyncScreenAsync(Guid screenId)
    {
        var screen = await db
            .Screens.Include(d => d.CampaignAssignments)
                .ThenInclude(ca => ca.Campaign.CampaignAssets)
                    .ThenInclude(ca => ca.Asset)
            .AsSplitQuery()
            .FirstOrDefaultAsync(d => d.Id == screenId);

        if (screen == null)
        {
            logger.LogWarning("Screen {ScreenId} not found", screenId);
            return;
        }

        if (screen.UserId == null)
        {
            logger.LogWarning("Screen {ScreenId} has no UserId, skipping sync", screenId);
            return;
        }

        var campaigns = BuildCampaignList(screen);
        if (campaigns.Count == 0)
            campaigns = await BuildDefaultCampaignListAsync();
        var config = BuildScreenConfiguration(screen, campaigns);

        logger.LogInformation(
            "SYNC SCREEN: {ScreenName} - CampaignAssignments: {Count}, Active campaigns: {ConfigCampaigns}, UserId: {UserId}",
            screen.Name,
            screen.CampaignAssignments.Count,
            campaigns.Count,
            screen.UserId
        );

        await hubContext.SendConfigurationUpdateAsync(screen.UserId, config);
        await NotifyAssetSyncAsync(screen, screen.UserId, campaigns);
    }

    public async Task<Guid?> GetScreenIdByUserIdAsync(string userId)
    {
        var screen = await db.Screens.FirstOrDefaultAsync(d => d.UserId == userId);
        return screen?.Id;
    }

    public async Task<bool> SendCommandAsync(Guid screenId, string command)
    {
        var screen = await db.Screens.FirstOrDefaultAsync(d => d.Id == screenId);
        if (screen?.UserId == null)
        {
            logger.LogWarning(
                "Cannot send command '{Command}' to screen {ScreenId}: screen not found or has no associated user",
                command,
                screenId
            );
            return false;
        }

        await hubContext.SendCommandAsync(screen.UserId, command);
        logger.LogInformation("Sent command '{Command}' to screen {ScreenId}", command, screenId);
        return true;
    }

    private static List<CampaignDetail> BuildCampaignList(Screen screen)
    {
        var utcNow = DateTime.UtcNow;
        return EffectiveCampaigns(screen)
            .Where(c => c.IsActiveAt(utcNow))
            .OrderByDescending(c => c.Priority)
            .ThenBy(c => c.Name)
            .Select(MapCampaign)
            .ToList();
    }

    private static IEnumerable<Database.Models.Campaign> EffectiveCampaigns(Screen screen) =>
        screen.CampaignAssignments.Select(ca => ca.Campaign);

    /// <summary>
    ///     Builds the playlist from the global default (fallback) campaign, when one is configured and
    ///     active. Used for screens that have no other active campaign assigned.
    /// </summary>
    private async Task<List<CampaignDetail>> BuildDefaultCampaignListAsync()
    {
        var utcNow = DateTime.UtcNow;
        var defaultCampaign = await db
            .Campaigns.Include(c => c.CampaignAssets)
                .ThenInclude(ca => ca.Asset)
            .Where(c => c.IsDefault)
            .FirstOrDefaultAsync();

        if (defaultCampaign == null || !defaultCampaign.IsActiveAt(utcNow))
            return [];

        return [MapCampaign(defaultCampaign)];
    }

    private static CampaignDetail MapCampaign(Database.Models.Campaign c) =>
        new(
            c.Id,
            c.Name,
            c.Description,
            c.CampaignAssets.OrderBy(a => a.Position)
                .Select(a => new CampaignAssetDetail(
                    a.Id,
                    a.AssetId,
                    a.Asset.Name,
                    a.Asset.Type,
                    a.Asset.Source,
                    a.Position,
                    a.DurationSeconds,
                    AssetDurationResolver.Resolve(a.Asset, a.DurationSeconds),
                    a.Asset.IsMuted,
                    a.Asset.ImageFit
                ))
                .ToList(),
            [],
            c.CreatedAt,
            c.UpdatedAt
        );

    private static ScreenConfiguration BuildScreenConfiguration(
        Screen screen,
        List<CampaignDetail> campaigns
    ) =>
        new()
        {
            ScreenId = screen.Id,
            ScreenName = screen.Name,
            Description = screen.Description,
            Location = screen.Location,
            ApprovalStatus = screen.ApprovalStatus.ToString(),
            ResolutionWidth = screen.ResolutionWidth,
            ResolutionHeight = screen.ResolutionHeight,
            ShufflePlayback = screen.ShufflePlayback,
            Campaigns = campaigns,
        };

    private async Task NotifyAssetSyncAsync(
        Screen screen,
        string userId,
        List<CampaignDetail> campaigns
    )
    {
        var allAssetIds = campaigns
            .SelectMany(c => c.Assets)
            .Select(a => a.AssetId)
            .Distinct()
            .ToList();

        await assetSyncService.CleanupSyncStatusAsync(screen.Id, allAssetIds);
        await assetSyncService.InitializeSyncStatusForScreenAsync(screen.Id, allAssetIds);

        var campaignsToSync = await assetSyncService.GetCampaignsToSyncAsync(screen.Id);
        await hubContext.StartAssetSyncAsync(userId, campaignsToSync);

        logger.LogInformation(
            "NOTIFY SYNC: {CampaignCount} campaigns, {AssetCount} assets",
            campaignsToSync.Count,
            allAssetIds.Count
        );
    }
}
