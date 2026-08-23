using Mireya.Database.Models;

namespace Mireya.Application.Services.Campaign;

public record CreateCampaignRequest(
    string Name,
    string? Description,
    List<CampaignAssetDto> Assets
);

public record UpdateCampaignRequest(
    string Name,
    string? Description,
    List<CampaignAssetDto> Assets
);

public record CampaignAssetDto(Guid AssetId, int Position, int? DurationSeconds);

public record CampaignAssignmentRequest(
    Guid CampaignId,
    bool IsEnabled = true,
    DateTime? StartDateUtc = null,
    DateTime? EndDateUtc = null
);

public record CampaignSummary(
    Guid Id,
    string Name,
    string? Description,
    int AssetCount,
    int ScreenCount,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int ActiveAssignmentCount = 0
);

public record CampaignDetail(
    Guid Id,
    string Name,
    string? Description,
    List<CampaignAssetDetail> Assets,
    List<CampaignAssignmentDetail> Assignments,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CampaignAssignmentDetail(
    Guid Id,
    Guid CampaignId,
    string CampaignName,
    Guid ScreenId,
    string ScreenName,
    string ScreenLocation,
    bool IsEnabled,
    DateTime? StartDateUtc,
    DateTime? EndDateUtc,
    bool IsActive
);

public record CampaignAssetDetail(
    Guid Id,
    Guid AssetId,
    string AssetName,
    AssetType AssetType,
    string Source,
    int Position,
    int? DurationSeconds,
    int ResolvedDuration,
    bool IsMuted,
    ImageFit ImageFit = ImageFit.Contain
);
