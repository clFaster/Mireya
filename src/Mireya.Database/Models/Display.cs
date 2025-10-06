using System.ComponentModel.DataAnnotations;

namespace Mireya.Database.Models;

/// <summary>
/// Represents a digital signage display device
/// </summary>
public class Display
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(100)]
    public string Location { get; set; } = string.Empty;

    /// <summary>
    /// Unique identifier for the physical device (e.g., MAC address, serial number)
    /// </summary>
    [MaxLength(100)]
    public string? DeviceIdentifier { get; set; }

    /// <summary>
    /// Screen resolution width in pixels
    /// </summary>
    public int? ResolutionWidth { get; set; }

    /// <summary>
    /// Screen resolution height in pixels
    /// </summary>
    public int? ResolutionHeight { get; set; }

    /// <summary>
    /// Indicates if the display is currently active/online
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Last time the display checked in or was seen online
    /// </summary>
    public DateTime? LastSeenAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
