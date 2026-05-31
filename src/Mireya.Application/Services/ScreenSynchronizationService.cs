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
    Task SyncScreenAsync(Guid displayId);
    Task SyncScreensAsync(IEnumerable<Guid> displayIds);
    Task<Guid?> GetDisplayIdByUserIdAsync(string userId);
    Task<bool> SendCommandAsync(Guid displayId, string command);
}

public class ScreenSynchronizationService(
    MireyaDbContext db,
    IScreenHubContext hubContext,
    IAssetSyncService assetSyncService,
    ILogger<ScreenSynchronizationService> logger
) : IScreenSynchronizationService
{

    public async Task SyncScreensAsync(IEnumerable<Guid> displayIds)
    {
        // Sequential execution required: DbContext is not thread-safe
        foreach (var displayId in displayIds.Distinct())
            await SyncScreenAsync(displayId);
    }

    public async Task SyncScreenAsync(Guid displayId)
    {
        var display = await db
            .Displays.Include(d => d.CampaignAssignments)
                .ThenInclude(ca => ca.Campaign)
                    .ThenInclude(c => c.CampaignAssets)
                        .ThenInclude(ca => ca.Asset)
            .FirstOrDefaultAsync(d => d.Id == displayId);

        if (display == null)
        {
            logger.LogWarning("Screen {DisplayId} not found", displayId);
            return;
        }

        if (display.UserId == null)
        {
            logger.LogWarning("Screen {DisplayId} has no UserId, skipping sync", displayId);
            return;
        }

        var campaigns = BuildCampaignList(display);
        if (campaigns.Count == 0)
            campaigns = await BuildDefaultCampaignListAsync();
        var config = BuildScreenConfiguration(display, campaigns);

        logger.LogInformation(
            "SYNC SCREEN: {ScreenName} - CampaignAssignments: {Count}, Active campaigns: {ConfigCampaigns}, UserId: {UserId}",
            display.Name,
            display.CampaignAssignments.Count,
            campaigns.Count,
            display.UserId
        );

        await hubContext.SendConfigurationUpdateAsync(display.UserId, config);
        await NotifyAssetSyncAsync(display, campaigns);
    }

    public async Task<Guid?> GetDisplayIdByUserIdAsync(string userId)
    {
        var display = await db.Displays.FirstOrDefaultAsync(d => d.UserId == userId);
        return display?.Id;
    }

    public async Task<bool> SendCommandAsync(Guid displayId, string command)
    {
        var display = await db.Displays.FirstOrDefaultAsync(d => d.Id == displayId);
        if (display?.UserId == null)
        {
            logger.LogWarning(
                "Cannot send command '{Command}' to screen {DisplayId}: screen not found or has no associated user",
                command, displayId);
            return false;
        }

        await hubContext.SendCommandAsync(display.UserId, command);
        logger.LogInformation("Sent command '{Command}' to screen {DisplayId}", command, displayId);
        return true;
    }

    private static List<CampaignDetail> BuildCampaignList(Display display)
    {
        var utcNow = DateTime.UtcNow;
        return display.CampaignAssignments
            .Select(ca => ca.Campaign)
            .Where(c => c.IsActiveAt(utcNow))
            .OrderByDescending(c => c.Priority)
            .ThenBy(c => c.Name)
            .Select(MapCampaign)
            .ToList();
    }

    /// <summary>
    ///     Builds the playlist from the global default (fallback) campaign, when one is configured and
    ///     active. Used for screens that have no other active campaign assigned.
    /// </summary>
    private async Task<List<CampaignDetail>> BuildDefaultCampaignListAsync()
    {
        var utcNow = DateTime.UtcNow;
        var defaultCampaign = await db.Campaigns
            .Include(c => c.CampaignAssets)
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

    private static ScreenConfiguration BuildScreenConfiguration(Display display, List<CampaignDetail> campaigns) =>
        new()
        {
            DisplayId = display.Id,
            ScreenName = display.Name,
            Description = display.Description,
            Location = display.Location,
            ApprovalStatus = display.ApprovalStatus.ToString(),
            ResolutionWidth = display.ResolutionWidth,
            ResolutionHeight = display.ResolutionHeight,
            ShufflePlayback = display.ShufflePlayback,
            Campaigns = campaigns,
        };

    private async Task NotifyAssetSyncAsync(Display display, List<CampaignDetail> campaigns)
    {
        var allAssetIds = campaigns
            .SelectMany(c => c.Assets)
            .Select(a => a.AssetId)
            .Distinct()
            .ToList();

        await assetSyncService.CleanupSyncStatusAsync(display.Id, allAssetIds);
        await assetSyncService.InitializeSyncStatusForDisplayAsync(display.Id, allAssetIds);

        var campaignsToSync = await assetSyncService.GetCampaignsToSyncAsync(display.Id);
        await hubContext.StartAssetSyncAsync(display.UserId!, campaignsToSync);

        logger.LogInformation(
            "NOTIFY SYNC: {CampaignCount} campaigns, {AssetCount} assets",
            campaignsToSync.Count,
            allAssetIds.Count
        );
    }
}
