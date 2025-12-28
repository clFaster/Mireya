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
    ///     File size in bytes (for uploaded files)
    /// </summary>
    public long? FileSizeBytes { get; set; }

    /// <summary>
    ///     Duration in seconds (for video assets)
    /// </summary>
    public int? DurationSeconds { get; set; }

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
