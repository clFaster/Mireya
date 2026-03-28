namespace Mireya.Api.Services.AssetSync;

public record AssetSyncStatusDto(
    Guid AssetId,
    string SyncState,
    int Progress,
    string? ErrorMessage
);

public record UpdateAssetSyncRequest(
    Guid AssetId,
    string SyncState,
    int Progress,
    string? ErrorMessage = null
);

public record AssetDownloadInfo(
    Guid AssetId,
    string Name,
    string Type,
    string Source,
    long? FileSizeBytes,
    int? DurationSeconds,
    bool IsMuted
);

public record CampaignSyncInfo(
    Guid CampaignId,
    string CampaignName,
    List<AssetDownloadInfo> Assets
);
