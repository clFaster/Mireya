namespace Mireya.Application.Services.ScreenManagement;

/// <summary>
///     Response payload for screen approval
/// </summary>
public class ApproveScreenResponse
{
    public required ScreenDetailsResponse Screen { get; set; }
}
