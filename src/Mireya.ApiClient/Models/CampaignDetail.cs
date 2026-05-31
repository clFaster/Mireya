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
    bool IsMuted,
    ImageFit ImageFit = ImageFit.Contain
);

/// <summary>
///     How an image is scaled to fit the screen. Numeric values must match the server's
///     <c>Mireya.Database.Models.ImageFit</c> for correct deserialization.
/// </summary>
public enum ImageFit
{
    Contain = 0,
    Cover = 1,
    Fill = 2,
}
