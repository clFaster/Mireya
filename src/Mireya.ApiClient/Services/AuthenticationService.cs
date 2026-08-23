using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Mireya.ApiClient.Data;
using Mireya.ApiClient.Generated;

namespace Mireya.ApiClient.Services;

/// <summary>
///     Implementation of authentication service for screen clients
///     Uses database-backed credential storage per backend
/// </summary>
public class AuthenticationService(
    IMireyaApiClient apiClient,
    ICredentialRepository credentials,
    IBackendManager backendManager,
    IScreenHubService hubService,
    ILogger<AuthenticationService> logger
) : IAuthenticationService
{
    public async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            await credentials.MigrateLegacyCredentialsAsync();

            var backend = await backendManager.GetCurrentBackendAsync();
            if (backend == null)
            {
                logger.LogDebug("No backend configured");
                return AuthenticationState.NotRegistered;
            }

            var credential = await credentials.GetCredentialsAsync(backend.Id);
            if (credential == null || string.IsNullOrEmpty(credential.Username))
            {
                logger.LogDebug("No registration found for backend {BackendId}", backend.Id);
                return AuthenticationState.NotRegistered;
            }

            if (!await credentials.HasValidCredentialsAsync(backend.Id))
                return AuthenticationState.NotAuthenticated;

            // Local expiry alone cannot prove that a token is still valid. The backend may
            // have been reset, or the screen user may have been deleted from the last run.
            try
            {
                await apiClient.GetApiScreenmanagementBonjourAsync();
            }
            catch (ApiException ex) when (ex.StatusCode is 302 or 401 or 403)
            {
                logger.LogInformation(
                    ex,
                    "Stored token was rejected by backend {BackendId}; login recovery required",
                    backend.Id
                );
                return AuthenticationState.NotAuthenticated;
            }
            catch (ApiException ex) when (ex.StatusCode == 404)
            {
                logger.LogInformation(
                    ex,
                    "Screen registration no longer exists on backend {BackendId}",
                    backend.Id
                );
                await credentials.DeleteCredentialsAsync(backend.Id);
                return AuthenticationState.NotRegistered;
            }
            catch (Exception ex)
            {
                // Connectivity failures must not cause a replacement screen to be created.
                // Let SignalR's normal retry policy handle an unavailable backend.
                logger.LogWarning(
                    ex,
                    "Could not validate stored token for backend {BackendId}; keeping local authentication state",
                    backend.Id
                );
            }

            logger.LogDebug("Valid credentials found for backend {BackendId}", backend.Id);
            return AuthenticationState.Authenticated;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking authentication state");
            return AuthenticationState.Failed;
        }
    }

    public async Task<RegisterResult> RegisterAsync(string? deviceName = null)
    {
        try
        {
            var backend = await backendManager.GetCurrentBackendAsync();
            if (backend == null)
                return new RegisterResult(
                    false,
                    null,
                    null,
                    "No backend configured. Please select a backend first."
                );

            // Generate credentials on the client
            var username = GenerateUsername();
            var password = GeneratePassword();

            logger.LogInformation("Registering screen with backend {BackendId}", backend.Id);

            // Register with backend
            var request = new RegisterScreenRequest
            {
                Username = username,
                Password = password,
                DeviceName = deviceName,
                ResolutionWidth = null,
                ResolutionHeight = null,
            };

            var response = await apiClient.PostApiScreenmanagementRegisterAsync(request);

            logger.LogInformation(
                "Registration successful. Screen identifier: {ScreenIdentifier}",
                response.ScreenIdentifier
            );

            await credentials.SaveRegistrationAsync(backend.Id, username, password);

            return new RegisterResult(true, response.ScreenIdentifier, response.UserId, null);
        }
        catch (ApiException ex)
        {
            logger.LogError(ex, "Registration failed");
            return new RegisterResult(false, null, null, $"Registration failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during registration");
            return new RegisterResult(false, null, null, $"Unexpected error: {ex.Message}");
        }
    }

    public async Task<LoginResult> LoginAsync()
    {
        try
        {
            var backend = await backendManager.GetCurrentBackendAsync();
            if (backend == null)
                return new LoginResult(
                    false,
                    null,
                    "No backend configured. Please select a backend first."
                );

            logger.LogInformation(
                "Attempting login for backend {BackendId} - {BaseUrl}",
                backend.Id,
                backend.BaseUrl
            );

            var credential = await credentials.GetCredentialsAsync(backend.Id);
            if (credential == null || string.IsNullOrEmpty(credential.Username))
                return new LoginResult(false, null, "No credentials found. Please register first.");

            var (response, errorMessage) = await AcquireAccessTokenAsync(backend.Id, credential);
            if (response == null)
                return new LoginResult(false, null, errorMessage ?? "Login failed.");

            await credentials.SaveTokensAsync(
                backend.Id,
                response.AccessToken,
                response.RefreshToken,
                DateTime.UtcNow.AddSeconds(response.ExpiresIn)
            );

            await hubService.ConnectAsync();
            logger.LogInformation(
                "Login succeeded for backend {BackendId}: credentials saved, connected to hub",
                backend.Id
            );

            return new LoginResult(true, response.AccessToken, null);
        }
        catch (ApiException ex)
        {
            logger.LogError(ex, "Login failed");
            return new LoginResult(false, null, $"Login failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error during login");
            return new LoginResult(false, null, $"Unexpected error: {ex.Message}");
        }
    }

    private async Task<(
        AccessTokenResponse? Response,
        string? ErrorMessage
    )> AcquireAccessTokenAsync(Guid backendId, BackendCredential credential)
    {
        AccessTokenResponse? response = null;

        if (!string.IsNullOrEmpty(credential.RefreshToken))
            response = await TryRefreshAsync(credential.RefreshToken, backendId);

        if (
            response == null
            && credential
                is {
                    Username: { Length: > 0 } storedUsername,
                    Password: { Length: > 0 } storedPassword,
                }
        )
        {
            response = await TryLoginWithPasswordAsync(storedUsername, storedPassword, backendId);
        }

        if (response != null)
            return (response, null);

        return await RegisterReplacementAndLoginAsync(backendId);
    }

    private async Task<AccessTokenResponse?> TryLoginWithPasswordAsync(
        string username,
        string password,
        Guid backendId
    )
    {
        try
        {
            return await LoginWithPasswordAsync(username, password);
        }
        catch (ApiException ex) when (ex.StatusCode == 401)
        {
            logger.LogWarning(
                ex,
                "Backend {BackendId} no longer accepts the stored screen identity; registering a replacement",
                backendId
            );
            return null;
        }
    }

    private async Task<(
        AccessTokenResponse? Response,
        string? ErrorMessage
    )> RegisterReplacementAndLoginAsync(Guid backendId)
    {
        // Refresh and password login have both been rejected, or an older installation
        // has no recoverable backend-scoped password. Replace only this backend's
        // registration; credentials for other servers are untouched.
        await credentials.DeleteCredentialsAsync(backendId);
        var registration = await RegisterAsync();
        if (!registration.Success)
            return (null, registration.ErrorMessage);

        var replacement = await credentials.GetCredentialsAsync(backendId);
        if (
            replacement
            is not {
                Username: { Length: > 0 } replacementUsername,
                Password: { Length: > 0 } replacementPassword,
            }
        )
        {
            return (null, "Replacement registration did not persist credentials.");
        }

        var response = await LoginWithPasswordAsync(replacementUsername, replacementPassword);
        return (response, null);
    }

    private async Task<AccessTokenResponse?> TryRefreshAsync(string refreshToken, Guid backendId)
    {
        try
        {
            logger.LogDebug("Refreshing access token for backend {BackendId}", backendId);
            return await apiClient.PostRefreshAsync(
                new RefreshRequest { RefreshToken = refreshToken }
            );
        }
        catch (ApiException ex) when (ex.StatusCode is 400 or 401)
        {
            logger.LogInformation(
                ex,
                "Refresh token was rejected for backend {BackendId}; trying the stored password",
                backendId
            );
            return null;
        }
    }

    private Task<AccessTokenResponse> LoginWithPasswordAsync(string username, string password)
    {
        // The Identity API field is named Email, but PasswordSignInAsync resolves UserName.
        var request = new LoginRequest { Email = username, Password = password };
        return apiClient.PostLoginAsync(false, false, request);
    }

    public async Task<ScreenInfo?> GetScreenInfoAsync()
    {
        try
        {
            var backend = await backendManager.GetCurrentBackendAsync();
            if (backend == null)
            {
                logger.LogWarning("Cannot fetch screen info: No backend configured");
                return null;
            }

            var credential = await credentials.GetCredentialsAsync(backend.Id);
            if (credential == null || string.IsNullOrEmpty(credential.AccessToken))
            {
                logger.LogWarning(
                    "Cannot fetch screen info: Not authenticated for backend {BackendId}",
                    backend.Id
                );
                return null;
            }

            logger.LogDebug("Fetching screen info for backend {BackendId}", backend.Id);

            var response = await apiClient.GetApiScreenmanagementBonjourAsync();

            logger.LogInformation(
                "Successfully fetched screen info: {ScreenIdentifier}",
                response.ScreenIdentifier
            );

            return new ScreenInfo(
                response.ScreenIdentifier,
                response.ScreenName,
                response.Description,
                response.ApprovalStatus
            );
        }
        catch (ApiException ex)
        {
            logger.LogError(
                ex,
                "API error fetching screen info. Status: {StatusCode}",
                ex.StatusCode
            );
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error fetching screen info");
            return null;
        }
    }

    public async Task LogoutAsync()
    {
        try
        {
            var backend = await backendManager.GetCurrentBackendAsync();
            if (backend == null)
            {
                logger.LogWarning("Cannot logout: No backend configured");
                return;
            }

            logger.LogInformation("Logging out from backend {BackendId}", backend.Id);

            // Disconnect from SignalR Hub
            await hubService.DisconnectAsync();
            logger.LogDebug("Disconnected from SignalR hub");

            // Delete credentials from database
            await credentials.DeleteCredentialsAsync(backend.Id);
            logger.LogInformation("Credentials deleted for backend {BackendId}", backend.Id);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Logout failed for backend. See inner exception for details.",
                ex
            );
        }
    }

    public string? GetAccessToken() => credentials.GetAccessToken();

    private static string GenerateUsername()
    {
        return $"screen-{Guid.NewGuid():N}";
    }

    private static string GeneratePassword()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }
}
