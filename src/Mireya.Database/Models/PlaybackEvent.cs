using System.ComponentModel.DataAnnotations;

namespace Mireya.Database.Models;

/// <summary>
///     An immutable "proof of play" record: a screen reported that it started showing a
///     specific asset at a point in time. Names are snapshotted so reports remain meaningful
///     even after the screen or asset is later renamed or deleted.
/// </summary>
public class PlaybackEvent
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    ///     The screen that played the asset.
    /// </summary>
    public Guid DisplayId { get; set; }

    public Display? Display { get; set; }

    /// <summary>
    ///     Screen name captured at play time.
    /// </summary>
    [MaxLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    ///     The asset that was shown. Nullable because the asset may be deleted afterwards.
    /// </summary>
    public Guid? AssetId { get; set; }

    /// <summary>
    ///     Asset name captured at play time.
    /// </summary>
    [MaxLength(255)]
    public string? AssetName { get; set; }

    /// <summary>
    ///     UTC instant the asset started playing on the screen.
    /// </summary>
    public DateTime PlayedAtUtc { get; set; } = DateTime.UtcNow;
}
