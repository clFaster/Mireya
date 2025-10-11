namespace Mireya.Api.Services.ScreenManagement;

/// <summary>
/// Response payload for screen registration
/// </summary>
public class RegisterScreenResponse
{
    /// <summary>
    /// The unique identifier for the screen
    /// </summary>
    public string ScreenIdentifier { get; set; } = string.Empty;
    
    /// <summary>
    /// Name of the screen
    /// </summary>
    public string ScreenName { get; set; } = string.Empty;
}