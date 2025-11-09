using System.ComponentModel.DataAnnotations;

namespace Mireya.Api.Services.ScreenManagement;

/// <summary>
/// Request payload for screen registration
/// </summary>
public class RegisterScreenRequest
{
    /// <summary>
    /// Client-generated username (GUID or device identifier)
    /// </summary>
    [Required]
    [MaxLength(256)]
    public required string Username { get; set; }
    
    /// <summary>
    /// Client-generated secure password
    /// </summary>
    [Required]
    [MinLength(9)]
    public required string Password { get; set; }
    
    /// <summary>
    /// Optional device Name
    /// </summary>
    public string? DeviceName { get; set; }
    
    /// <summary>
    /// Screen resolution width in pixels
    /// </summary>
    public int? ResolutionWidth { get; set; }
    
    /// <summary>
    /// Screen resolution height in pixels
    /// </summary>
    public int? ResolutionHeight { get; set; }
}