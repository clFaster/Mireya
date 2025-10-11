namespace Mireya.Api.Services.ScreenManagement;

/// <summary>
/// Request payload for updating screen details
/// </summary>
public class UpdateScreenRequest
{
    /// <summary>
    /// Screen name
    /// </summary>
    public string? Name { get; set; }
    
    /// <summary>
    /// Screen description
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Screen location
    /// </summary>
    public string? Location { get; set; }
}