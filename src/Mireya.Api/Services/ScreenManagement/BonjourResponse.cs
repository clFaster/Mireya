namespace Mireya.Api.Services.ScreenManagement;

/// <summary>
/// Response payload for the Bonjour endpoint
/// </summary>
public class BonjourResponse
{
    /// <summary>
    /// The unique identifier for the screen
    /// </summary>
    public required string ScreenIdentifier { get; set; }
    
    /// <summary>
    /// Name of the screen
    /// </summary>
    public required string ScreenName { get; set; }
    
    /// <summary>
    /// Description of the screen
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Approval status (Pending, Approved, Rejected)
    /// </summary>
    public required string ApprovalStatus { get; set; }
    
    /// <summary>
    /// Physical location of the screen
    /// </summary>
    public string? Location { get; set; }
}
