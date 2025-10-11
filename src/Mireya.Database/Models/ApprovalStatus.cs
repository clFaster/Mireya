namespace Mireya.Database.Models;

/// <summary>
/// Approval status for display devices
/// </summary>
public enum ApprovalStatus
{
    /// <summary>
    /// Display has registered but is awaiting admin approval
    /// </summary>
    Pending = 0,
    
    /// <summary>
    /// Display has been approved and can operate
    /// </summary>
    Approved = 1,
    
    /// <summary>
    /// Display has been rejected by admin
    /// </summary>
    Rejected = 2
}