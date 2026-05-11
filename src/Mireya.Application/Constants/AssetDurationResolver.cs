using Mireya.Database.Models;

namespace Mireya.Application.Constants;

/// <summary>
///     Shared helper for resolving asset display duration
/// </summary>
public static class AssetDurationResolver
{
    public const int DefaultDurationSeconds = 10;

    /// <summary>
    ///     Resolves the effective duration for an asset:
    ///     1. Campaign-level override (if positive)
    ///     2. Video intrinsic duration (if video with positive duration)
    ///     3. Default (10 seconds)
    /// </summary>
    public static int Resolve(Asset asset, int? campaignDuration)
    {
        if (campaignDuration > 0)
            return campaignDuration.Value;

        if (asset.Type == AssetType.Video && asset.DurationSeconds > 0)
            return asset.DurationSeconds.Value;

        return DefaultDurationSeconds;
    }
}
