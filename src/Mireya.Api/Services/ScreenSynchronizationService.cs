using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Mireya.Api.Hubs;
using Mireya.Api.Services.AssetSync;
using Mireya.Api.Services.Campaign;
using Mireya.Api.Services.ScreenManagement;
using Mireya.Database;
using Mireya.Database.Models;

namespace Mireya.Api.Services;

public interface IScreenSynchronizationService
{
    Task SyncScreenAsync(Guid displayId);
    Task SyncScreensAsync(IEnumerable<Guid> displayIds);
}

public class ScreenSynchronizationService(
    MireyaDbContext db,
    IHubContext<ScreenHub, IScreenClient> hubContext,
    IAssetSyncService assetSyncService,
    ILogger<ScreenSynchronizationService> logger
) : IScreenSynchronizationService
{
    private const int DefaultAssetDuration = 10;

    public async Task SyncScreensAsync(IEnumerable<Guid> displayIds)
    {
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

        var campaigns = display
            .CampaignAssignments.Select(ca => ca.Campaign)
            .Select(c => new CampaignDetail(
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
                        ResolveAssetDuration(a.Asset, a.DurationSeconds)
                    ))
                    .ToList(),
                [], // Displays list not needed for screen client
                c.CreatedAt,
                c.UpdatedAt
            ))
            .ToList();

        var config = new ScreenConfiguration
        {
            DisplayId = display.Id,
            ScreenName = display.Name,
            Description = display.Description,
            Location = display.Location,
            ApprovalStatus = display.ApprovalStatus.ToString(),
            ResolutionWidth = display.ResolutionWidth,
            ResolutionHeight = display.ResolutionHeight,
            Campaigns = campaigns,
        };

        logger.LogInformation(
            "SYNC SCREEN: {ScreenName} - CampaignAssignments: {Count}, Config campaigns: {ConfigCampaigns}, UserId: {UserId}",
            display.Name,
            display.CampaignAssignments.Count,
            campaigns.Count,
            display.UserId
        );

        await hubContext.Clients.User(display.UserId).ReceiveConfigurationUpdate(config);

        // Get all unique asset IDs from all campaigns
        var allAssetIds = campaigns
            .SelectMany(c => c.Assets)
            .Select(a => a.AssetId)
            .Distinct()
            .ToList();

        // Initialize sync status for all assets and cleanup old ones
        await assetSyncService.CleanupSyncStatusAsync(displayId, allAssetIds);
        await assetSyncService.InitializeSyncStatusForDisplayAsync(displayId, allAssetIds);

        // Notify client to start asset sync
        var campaignsToSync = await assetSyncService.GetCampaignsToSyncAsync(displayId);
        await hubContext.Clients.User(display.UserId).StartAssetSync(campaignsToSync);

        logger.LogInformation(
            "NOTIFY SYNC: {CampaignCount} campaigns, {AssetCount} assets",
            campaignsToSync.Count,
            allAssetIds.Count
        );
    }

    private static int ResolveAssetDuration(Database.Models.Asset asset, int? campaignDuration)
    {
        if (campaignDuration > 0)
            return campaignDuration.Value;

        if (asset.Type == AssetType.Video && asset.DurationSeconds > 0)
            return asset.DurationSeconds.Value;

        return DefaultAssetDuration;
    }
}
