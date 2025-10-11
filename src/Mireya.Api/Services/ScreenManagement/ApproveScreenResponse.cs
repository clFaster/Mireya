namespace Mireya.Api.Services.ScreenManagement;

public class ApproveScreenResponse
{
    public required ScreenDetailsResponse Screen { get; set; }
    public required string Password { get; set; }
}