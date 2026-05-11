using Microsoft.EntityFrameworkCore;
using Mireya.Database;
using Mireya.Database.Models;

namespace Mireya.Application.Services.AssetSync;

public interface IAssetSyncService
{
    Task InitializeSyncStatusForDisplayAsync(Guid displayId, List<Guid> assetIds);
    Task UpdateAssetSyncStatusAsync(Guid displayId, UpdateAssetSyncRequest request);
    Task<List<AssetSyncStatusDto>> GetSyncStatusForDisplayAsync(Guid displayId);
    Task<List<CampaignSyncInfo>> GetCampaignsToSyncAsync(Guid displayId);
    Task CleanupSyncStatusAsync(Guid displayId, List<Guid> currentAssetIds);
    Task<Guid?> GetDisplayIdByUserIdAsync(string userId);
}

public class AssetSyncService(MireyaDbContext db, ILogger<AssetSyncService> logger)
    : IAssetSyncService
{
    public async Task InitializeSyncStatusForDisplayAsync(Guid displayId, List<Guid> assetIds)
    {
        logger.LogDebug(
            "Initializing sync status for display {DisplayId} with {AssetCount} assets",
            displayId,
            assetIds.Count
        );

        var distinctAssetIds = assetIds.Distinct().ToList();

        // Batch-query all existing sync statuses for this display upfront
        var existingAssetIdsList = await db.AssetSyncStatuses
            .Where(ass => ass.DisplayId == displayId && distinctAssetIds.Contains(ass.AssetId))
            .Select(ass => ass.AssetId)
            .ToListAsync();
        var existingAssetIds = existingAssetIdsList.ToHashSet();

        var newStatuses = distinctAssetIds
            .Where(assetId => !existingAssetIds.Contains(assetId))
            .Select(assetId => new AssetSyncStatus
            {
                DisplayId = displayId,
                AssetId = assetId,
                SyncState = SyncState.Pending,
                Progress = 0,
                LastUpdatedAt = DateTime.UtcNow,
            })
            .ToList();

        if (newStatuses.Count > 0)
        {
            db.AssetSyncStatuses.AddRange(newStatuses);
            await db.SaveChangesAsync();
            logger.LogDebug("Created {Count} new sync status entries", newStatuses.Count);
        }
    }

    public async Task UpdateAssetSyncStatusAsync(Guid displayId, UpdateAssetSyncRequest request)
    {
        logger.LogDebug(
            "Updating sync status for display {DisplayId}, asset {AssetId}: {State} ({Progress}%)",
            displayId,
            request.AssetId,
            request.SyncState,
            request.Progress
        );

        var syncStatus = await db.AssetSyncStatuses.FirstOrDefaultAsync(ass =>
            ass.DisplayId == displayId && ass.AssetId == request.AssetId
        );

        if (syncStatus == null)
        {
            logger.LogWarning(
                "Sync status not found for display {DisplayId}, asset {AssetId}",
                displayId,
                request.AssetId
            );
            return;
        }

        if (Enum.TryParse<SyncState>(request.SyncState, true, out var state))
        {
            syncStatus.SyncState = state;
        }
        else
        {
            logger.LogWarning("Invalid sync state: {State}", request.SyncState);
            return;
        }

        syncStatus.Progress = Math.Clamp(request.Progress, 0, 100);
        syncStatus.ErrorMessage = request.ErrorMessage;
        syncStatus.LastUpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        logger.LogInformation(
            "Updated sync status for display {DisplayId}, asset {AssetId}: {State} ({Progress}%)",
            displayId,
            request.AssetId,
            syncStatus.SyncState,
            syncStatus.Progress
        );
    }

    public async Task<List<AssetSyncStatusDto>> GetSyncStatusForDisplayAsync(Guid displayId)
    {
        var statuses = await db
            .AssetSyncStatuses.Where(ass => ass.DisplayId == displayId)
            .ToListAsync();

        return statuses
            .Select(ass => new AssetSyncStatusDto(
                ass.AssetId,
                ass.SyncState.ToString(),
                ass.Progress,
                ass.ErrorMessage
            ))
            .ToList();
    }

    public async Task<List<CampaignSyncInfo>> GetCampaignsToSyncAsync(Guid displayId)
    {
        var campaigns = await db
            .CampaignAssignments.Where(ca => ca.DisplayId == displayId)
            .Include(ca => ca.Campaign)
                .ThenInclude(c => c.CampaignAssets)
                    .ThenInclude(ca => ca.Asset)
            .Select(ca => ca.Campaign)
            .ToListAsync();

        var result = new List<CampaignSyncInfo>();

        foreach (var campaign in campaigns)
        {
            var assets = campaign
                .CampaignAssets.Select(ca => new AssetDownloadInfo(
                    ca.Asset.Id,
                    ca.Asset.Name,
                    ca.Asset.Type.ToString(),
                    ca.Asset.Source,
                    ca.Asset.FileSizeBytes,
                    ca.Asset.DurationSeconds,
                    ca.Asset.IsMuted
                ))
                .ToList();

            result.Add(new CampaignSyncInfo(campaign.Id, campaign.Name, assets));
        }

        return result;
    }

    public async Task<Guid?> GetDisplayIdByUserIdAsync(string userId)
    {
        var display = await db.Displays.FirstOrDefaultAsync(d => d.UserId == userId);
        return display?.Id;
    }

    public async Task CleanupSyncStatusAsync(Guid displayId, List<Guid> currentAssetIds)
    {
        logger.LogDebug("Cleaning up sync status for display {DisplayId}", displayId);

        var outdatedStatuses = await db
            .AssetSyncStatuses.Where(ass =>
                ass.DisplayId == displayId && !currentAssetIds.Contains(ass.AssetId)
            )
            .ToListAsync();

        if (outdatedStatuses.Any())
        {
            db.AssetSyncStatuses.RemoveRange(outdatedStatuses);
            await db.SaveChangesAsync();

            logger.LogInformation(
                "Removed {Count} outdated sync status entries for display {DisplayId}",
                outdatedStatuses.Count,
                displayId
            );
        }
    }
}
