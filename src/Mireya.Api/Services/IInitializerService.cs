namespace Mireya.Api.Services;

/// <summary>
/// Service responsible for initializing the default admin user and roles in the system.
/// </summary>
public interface IInitializerService
{
    /// <summary>
    /// Initializes the admin role and default admin user if they don't exist.
    /// Also ensures existing users have the proper admin role assigned.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task InitializeAsync();
}
