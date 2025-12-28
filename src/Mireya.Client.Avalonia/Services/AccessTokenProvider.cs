using System.Threading.Tasks;
using Mireya.ApiClient.Services;

namespace Mireya.Client.Avalonia.Services;

/// <summary>
/// Database-backed access token provider
/// Retrieves tokens from encrypted credential storage
/// </summary>
public class AccessTokenProvider : IAccessTokenProvider
{
    private readonly ICredentialManager _credentialManager;

    public AccessTokenProvider(ICredentialManager credentialManager)
    {
        _credentialManager = credentialManager;
    }

    public string? GetAccessToken()
    {
        // This needs to be synchronous for HTTP client handlers
        // Use Task.Run to make it work (not ideal but necessary for IAccessTokenProvider interface)
        var credential = Task.Run(async () => 
            await _credentialManager.GetCurrentCredentialsAsync()).Result;
            
        return credential?.AccessToken;
    }

    public void SetAccessToken(string? token)
    {
        // This method is deprecated in favor of CredentialManager.SaveCredentialsAsync
        // Left empty for backward compatibility
        // Token management should be done via CredentialManager
    }
}

