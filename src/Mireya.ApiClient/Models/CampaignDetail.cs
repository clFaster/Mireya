using Mireya.ApiClient.Generated;

namespace Mireya.ApiClient.Models;

public record CampaignDetail(
    Guid Id,
    string Name,
    string? Description,
    List<CampaignAssetItem> Assets,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CampaignAssetItem(
    Guid Id,
    Guid AssetId,
    string AssetName,
    AssetType AssetType,
    string Source,
    int Position,
    int? DurationSeconds,
    int ResolvedDuration,
    bool IsMuted
);
