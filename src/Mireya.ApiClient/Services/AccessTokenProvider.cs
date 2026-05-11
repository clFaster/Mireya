namespace Mireya.ApiClient.Services;

/// <summary>
///     Database-backed access token provider
///     Retrieves tokens from encrypted credential storage
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
        // Use Task.Run to avoid deadlocks with synchronization contexts
        var credential = Task.Run(async () =>
            await _credentialManager.GetCurrentCredentialsAsync()
        ).Result;

        return credential?.AccessToken;
    }

    public void SetAccessToken(string? token)
    {
        // Deprecated in favor of CredentialManager.SaveCredentialsAsync
        // Left empty for backward compatibility
    }
}
