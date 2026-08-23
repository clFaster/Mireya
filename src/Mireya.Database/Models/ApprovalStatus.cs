namespace Mireya.Database.Models;

/// <summary>
///     Approval status for screen devices
/// </summary>
public enum ApprovalStatus
{
    /// <summary>
    ///     Screen has registered but is awaiting admin approval
    /// </summary>
    Pending = 0,

    /// <summary>
    ///     Screen has been approved and can operate
    /// </summary>
    Approved = 1,

    /// <summary>
    ///     Screen has been rejected by admin
    /// </summary>
    Rejected = 2,
}
