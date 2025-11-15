using System.Threading.Tasks;
using Mireya.ApiClient.Models;

namespace Mireya.Client.Avalonia.Services;

/// <summary>
/// Platform-specific credential storage interface
/// </summary>
public interface ICredentialStorage
{
    /// <summary>
    /// Store screen credentials securely
    /// </summary>
    Task SaveCredentialsAsync(Credentials credentials);
    
    /// <summary>
    /// Retrieve stored credentials
    /// </summary>
    Task<Credentials?> GetCredentialsAsync();
    
    /// <summary>
    /// Delete stored credentials
    /// </summary>
    Task DeleteCredentialsAsync();
    
    /// <summary>
    /// Check if credentials exist
    /// </summary>
    Task<bool> HasCredentialsAsync();
}
