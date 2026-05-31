using System.ComponentModel.DataAnnotations;

namespace Mireya.Database.Models;

/// <summary>
///     Represents a content asset (Image, Website, Video) for digital signage
/// </summary>
public class Asset
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    ///     Type of asset: Image, Website, Video
    /// </summary>
    [Required]
    public AssetType Type { get; set; }

    /// <summary>
    ///     URL or file path to the asset
    /// </summary>
    [Required]
    [MaxLength(2000)]
    public string Source { get; set; } = string.Empty;

    /// <summary>
    ///     URL or file path to a preview thumbnail (poster frame for videos).
    ///     For images this equals <see cref="Source" />; null when no preview is available.
    /// </summary>
    [MaxLength(2000)]
    public string? ThumbnailSource { get; set; }

    /// <summary>
    ///     Comma-separated tags used for organising and searching assets.
    /// </summary>
    [MaxLength(500)]
    public string? Tags { get; set; }

    /// <summary>
    ///     File size in bytes (for uploaded files)
    /// </summary>
    public long? FileSizeBytes { get; set; }

    /// <summary>
    ///     Duration in seconds (for video assets)
    /// </summary>
    public int? DurationSeconds { get; set; }

    /// <summary>
    ///     Whether the video audio should be muted (for video assets only)
    /// </summary>
    public bool IsMuted { get; set; }

    /// <summary>
    ///     How an image asset should be fitted to the screen when displayed.
    /// </summary>
    public ImageFit ImageFit { get; set; } = ImageFit.Contain;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
///     Enum representing the type of asset
/// </summary>
public enum AssetType
{
    Image = 1,
    Video = 2,
    Website = 3,
}

/// <summary>
///     How an image is scaled to fit the screen.
/// </summary>
public enum ImageFit
{
    /// <summary>Scale to fit entirely within the screen, preserving aspect ratio (letterboxed).</summary>
    Contain = 0,

    /// <summary>Scale to fill the whole screen, preserving aspect ratio (edges may be cropped).</summary>
    Cover = 1,

    /// <summary>Stretch to fill the whole screen, ignoring aspect ratio.</summary>
    Fill = 2,
}
