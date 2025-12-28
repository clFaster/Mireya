namespace Mireya.ApiClient.Models;

public class AssetSyncStatusDto
{
    public Guid AssetId { get; set; }
    public string SyncState { get; set; } = string.Empty;
    public int Progress { get; set; }
    public string? ErrorMessage { get; set; }
}

public class UpdateAssetSyncRequest
{
    public Guid AssetId { get; set; }
    public string SyncState { get; set; } = string.Empty;
    public int Progress { get; set; }
    public string? ErrorMessage { get; set; }
}

public class AssetDownloadInfo
{
    public Guid AssetId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public long? FileSizeBytes { get; set; }
}

public class CampaignSyncInfo
{
    public Guid CampaignId { get; set; }
    public string CampaignName { get; set; } = string.Empty;
    public List<AssetDownloadInfo> Assets { get; set; } = new();
}
