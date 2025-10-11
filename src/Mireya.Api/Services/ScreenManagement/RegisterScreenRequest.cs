namespace Mireya.Api.Services.ScreenManagement;

/// <summary>
/// Request payload for screen registration
/// </summary>
public class RegisterScreenRequest
{
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