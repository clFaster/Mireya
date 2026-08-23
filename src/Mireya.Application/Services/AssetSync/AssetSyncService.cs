using Microsoft.EntityFrameworkCore;
using Mireya.Database;
using Mireya.Database.Models;

namespace Mireya.Application.Services.AssetSync;

public interface IAssetSyncService
{
    Task InitializeSyncStatusForScreenAsync(Guid screenId, List<Guid> assetIds);
    Task<AssetSyncUpdateResult> UpdateAssetSyncStatusAsync(
        Guid screenId,
        UpdateAssetSyncRequest request
    );
    Task<List<AssetSyncStatusDto>> GetSyncStatusForScreenAsync(Guid screenId);
    Task<List<CampaignSyncInfo>> GetCampaignsToSyncAsync(Guid screenId);
    Task CleanupSyncStatusAsync(Guid screenId, List<Guid> currentAssetIds);
    Task<Guid?> GetScreenIdByUserIdAsync(string userId);
}

public enum AssetSyncUpdateResult
{
    Updated,
    NotFound,
    InvalidState,
}

public class AssetSyncService(MireyaDbContext db, ILogger<AssetSyncService> logger)
    : IAssetSyncService
{
    public async Task InitializeSyncStatusForScreenAsync(Guid screenId, List<Guid> assetIds)
    {
        logger.LogDebug(
            "Initializing sync status for screen {ScreenId} with {AssetCount} assets",
            screenId,
            assetIds.Count
        );

        var distinctAssetIds = assetIds.Distinct().ToList();

        // Batch-query all existing sync statuses for this screen upfront
        var existingAssetIdsList = await db
            .AssetSyncStatuses.Where(ass =>
                ass.ScreenId == screenId && distinctAssetIds.Contains(ass.AssetId)
            )
            .Select(ass => ass.AssetId)
            .ToListAsync();
        var existingAssetIds = existingAssetIdsList.ToHashSet();

        var newStatuses = distinctAssetIds
            .Where(assetId => !existingAssetIds.Contains(assetId))
            .Select(assetId => new AssetSyncStatus
            {
                ScreenId = screenId,
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

    public async Task<AssetSyncUpdateResult> UpdateAssetSyncStatusAsync(
        Guid screenId,
        UpdateAssetSyncRequest request
    )
    {
        logger.LogDebug(
            "Updating sync status for screen {ScreenId}, asset {AssetId}: {State} ({Progress}%)",
            screenId,
            request.AssetId,
            request.SyncState,
            request.Progress
        );

        var syncStatus = await db.AssetSyncStatuses.FirstOrDefaultAsync(ass =>
            ass.ScreenId == screenId && ass.AssetId == request.AssetId
        );

        if (syncStatus == null)
        {
            logger.LogWarning(
                "Sync status not found for screen {ScreenId}, asset {AssetId}",
                screenId,
                request.AssetId
            );
            return AssetSyncUpdateResult.NotFound;
        }

        if (Enum.TryParse<SyncState>(request.SyncState, true, out var state))
        {
            syncStatus.SyncState = state;
        }
        else
        {
            logger.LogWarning("Invalid sync state: {State}", request.SyncState);
            return AssetSyncUpdateResult.InvalidState;
        }

        syncStatus.Progress = Math.Clamp(request.Progress, 0, 100);
        syncStatus.ErrorMessage = request.ErrorMessage;
        syncStatus.LastUpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        logger.LogInformation(
            "Updated sync status for screen {ScreenId}, asset {AssetId}: {State} ({Progress}%)",
            screenId,
            request.AssetId,
            syncStatus.SyncState,
            syncStatus.Progress
        );

        return AssetSyncUpdateResult.Updated;
    }

    public async Task<List<AssetSyncStatusDto>> GetSyncStatusForScreenAsync(Guid screenId)
    {
        var statuses = await db
            .AssetSyncStatuses.Where(ass => ass.ScreenId == screenId)
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

    public async Task<List<CampaignSyncInfo>> GetCampaignsToSyncAsync(Guid screenId)
    {
        var campaigns = await db
            .CampaignAssignments.Where(ca => ca.ScreenId == screenId)
            .Include(ca => ca.Campaign.CampaignAssets)
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

    public async Task<Guid?> GetScreenIdByUserIdAsync(string userId)
    {
        var screen = await db.Screens.FirstOrDefaultAsync(d => d.UserId == userId);
        return screen?.Id;
    }

    public async Task CleanupSyncStatusAsync(Guid screenId, List<Guid> currentAssetIds)
    {
        logger.LogDebug("Cleaning up sync status for screen {ScreenId}", screenId);

        var outdatedStatuses = await db
            .AssetSyncStatuses.Where(ass =>
                ass.ScreenId == screenId && !currentAssetIds.Contains(ass.AssetId)
            )
            .ToListAsync();

        if (outdatedStatuses.Any())
        {
            db.AssetSyncStatuses.RemoveRange(outdatedStatuses);
            await db.SaveChangesAsync();

            logger.LogInformation(
                "Removed {Count} outdated sync status entries for screen {ScreenId}",
                outdatedStatuses.Count,
                screenId
            );
        }
    }
}
