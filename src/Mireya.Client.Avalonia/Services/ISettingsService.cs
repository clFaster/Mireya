using System.Threading.Tasks;

namespace Mireya.Client.Avalonia.Services;

/// <summary>
/// Service for managing application settings persistence
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Gets the backend URL from settings
    /// </summary>
    Task<string?> GetBackendUrlAsync();

    /// <summary>
    /// Saves the backend URL to settings
    /// </summary>
    Task SaveBackendUrlAsync(string url);

    /// <summary>
    /// Validates if a URL string is in a valid format
    /// </summary>
    bool IsValidUrl(string? url);
}
