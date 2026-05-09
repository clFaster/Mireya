using Microsoft.EntityFrameworkCore;
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
}

public class ScreenSynchronizationService(
    MireyaDbContext db,
    IScreenHubContext hubContext,
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

        var campaigns = BuildCampaignList(display);
        var config = BuildScreenConfiguration(display, campaigns);

        logger.LogInformation(
            "SYNC SCREEN: {ScreenName} - CampaignAssignments: {Count}, Config campaigns: {ConfigCampaigns}, UserId: {UserId}",
            display.Name,
            display.CampaignAssignments.Count,
            campaigns.Count,
            display.UserId
        );

        await hubContext.SendConfigurationUpdateAsync(display.UserId, config);
        await NotifyAssetSyncAsync(display, campaigns);
    }

    private static List<CampaignDetail> BuildCampaignList(Display display) =>
        display.CampaignAssignments
            .Select(ca => ca.Campaign)
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
                        ResolveAssetDuration(a.Asset, a.DurationSeconds),
                        a.Asset.IsMuted
                    ))
                    .ToList(),
                [],
                c.CreatedAt,
                c.UpdatedAt
            ))
            .ToList();

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

    private static int ResolveAssetDuration(Database.Models.Asset asset, int? campaignDuration)
    {
        if (campaignDuration > 0)
            return campaignDuration.Value;

        if (asset.Type == AssetType.Video && asset.DurationSeconds > 0)
            return asset.DurationSeconds.Value;

        return DefaultAssetDuration;
    }
}
