namespace Mireya.Api.Services.Campaign;

// Request DTOs
public record CreateCampaignRequest(
    string Name,
    string? Description,
    List<CampaignAssetDto> Assets,
    List<Guid> DisplayIds
);

public record UpdateCampaignRequest(
    string Name,
    string? Description,
    List<CampaignAssetDto> Assets,
    List<Guid> DisplayIds
);

public record CampaignAssetDto(
    Guid AssetId,
    int Position,
    int? DurationSeconds
);

// Response DTOs
public record CampaignSummary(
    Guid Id,
    string Name,
    string? Description,
    int AssetCount,
    int DisplayCount,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CampaignDetail(
    Guid Id,
    string Name,
    string? Description,
    List<CampaignAssetDetail> Assets,
    List<DisplayInfo> Displays,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CampaignAssetDetail(
    Guid Id,
    Guid AssetId,
    string AssetName,
    Database.Models.AssetType AssetType,
    string Source,
    int Position,
    int? DurationSeconds,
    int ResolvedDuration // Calculated: use DurationSeconds or asset's duration or default
);

public record DisplayInfo(
    Guid Id,
    string Name,
    string Location
);
