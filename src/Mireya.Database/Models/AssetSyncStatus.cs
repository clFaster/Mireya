using System.ComponentModel.DataAnnotations;

namespace Mireya.Database.Models;

/// <summary>
/// Tracks the sync status of assets for each display
/// </summary>
public class AssetSyncStatus
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid DisplayId { get; set; }

    [Required]
    public Guid AssetId { get; set; }

    /// <summary>
    /// Sync state: Pending, Downloading, Downloaded, Failed
    /// </summary>
    [Required]
    public SyncState SyncState { get; set; } = SyncState.Pending;

    /// <summary>
    /// Progress percentage (0-100)
    /// </summary>
    public int Progress { get; set; }

    /// <summary>
    /// Error message if sync failed
    /// </summary>
    [MaxLength(1000)]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Last time the sync state was updated
    /// </summary>
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Display Display { get; set; } = null!;
    public Asset Asset { get; set; } = null!;
}

public enum SyncState
{
    Pending = 0,
    Downloading = 1,
    Downloaded = 2,
    Failed = 3
}
