namespace Mireya.Api.Services.ScreenManagement;

/// <summary>
///     Response payload for screen registration
/// </summary>
public class RegisterScreenResponse
{
    /// <summary>
    ///     The unique identifier for the screen
    /// </summary>
    public required string ScreenIdentifier { get; set; }

    /// <summary>
    ///     User ID of the created user account
    /// </summary>
    public required string UserId { get; set; }

    /// <summary>
    ///     Name of the screen
    /// </summary>
    public required string ScreenName { get; set; }
}
