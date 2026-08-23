using Mireya.Database.Models;

namespace Mireya.Application.Services.Campaign;

// Request DTOs
public record CreateCampaignRequest(
    string Name,
    string? Description,
    List<CampaignAssetDto> Assets,
    List<Guid> ScreenIds,
    bool IsEnabled = true,
    DateTime? StartDateUtc = null,
    DateTime? EndDateUtc = null,
    int Priority = 0,
    bool IsDefault = false,
    int? RecurrenceDaysMask = null,
    TimeOnly? DailyStartTime = null,
    TimeOnly? DailyEndTime = null,
    string? RecurrenceTimeZoneId = null
);

public record UpdateCampaignRequest(
    string Name,
    string? Description,
    List<CampaignAssetDto> Assets,
    // null = leave screen assignments unchanged; a list (incl. empty) = set assignments to exactly this set
    List<Guid>? ScreenIds,
    bool IsEnabled = true,
    DateTime? StartDateUtc = null,
    DateTime? EndDateUtc = null,
    int Priority = 0,
    bool IsDefault = false,
    int? RecurrenceDaysMask = null,
    TimeOnly? DailyStartTime = null,
    TimeOnly? DailyEndTime = null,
    string? RecurrenceTimeZoneId = null
);

public record CampaignAssetDto(Guid AssetId, int Position, int? DurationSeconds);

// Response DTOs
public record CampaignSummary(
    Guid Id,
    string Name,
    string? Description,
    int AssetCount,
    int ScreenCount,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsEnabled,
    DateTime? StartDateUtc,
    DateTime? EndDateUtc,
    bool IsActive,
    int Priority = 0,
    bool IsDefault = false
);

public record CampaignDetail(
    Guid Id,
    string Name,
    string? Description,
    List<CampaignAssetDetail> Assets,
    List<ScreenInfo> Screens,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsEnabled = true,
    DateTime? StartDateUtc = null,
    DateTime? EndDateUtc = null,
    int Priority = 0,
    bool IsDefault = false,
    int? RecurrenceDaysMask = null,
    TimeOnly? DailyStartTime = null,
    TimeOnly? DailyEndTime = null,
    string? RecurrenceTimeZoneId = null
);

public record CampaignAssetDetail(
    Guid Id,
    Guid AssetId,
    string AssetName,
    AssetType AssetType,
    string Source,
    int Position,
    int? DurationSeconds,
    int ResolvedDuration, // Calculated: use DurationSeconds or asset's duration or default
    bool IsMuted, // Whether video audio should be muted
    ImageFit ImageFit = ImageFit.Contain // How an image is fitted to the screen
);

public record ScreenInfo(Guid Id, string Name, string Location);
