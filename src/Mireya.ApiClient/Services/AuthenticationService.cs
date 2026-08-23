using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Mireya.ApiClient.Data;
using Mireya.ApiClient.Generated;

namespace Mireya.ApiClient.Services;

/// <summary>
///     Implementation of authentication service for screen clients
///     Uses database-backed credential storage per backend
/// </summary>
public class AuthenticationService : IAuthenticationService
{
    private readonly IMireyaApiClient _apiClient;
    private readonly IBackendManager _backendManager;
    private readonly ICredentialRepository _credentials;
    private readonly IScreenHubService _hubService;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(
        IMireyaApiClient apiClient,
        ICredentialRepository credentials,
        IBackendManager backendManager,
        IScreenHubService hubService,
        ILogger<AuthenticationService> logger
    )
    {
        _apiClient = apiClient;
        _credentials = credentials;
        _backendManager = backendManager;
        _hubService = hubService;
        _logger = logger;
    }

    public async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            await _credentials.MigrateLegacyCredentialsAsync();

            var backend = await _backendManager.GetCurrentBackendAsync();
            if (backend == null)
            {
                _logger.LogDebug("No backend configured");
                return AuthenticationState.NotRegistered;
            }

            var credential = await _credentials.GetCredentialsAsync(backend.Id);
            if (credential == null || string.IsNullOrEmpty(credential.Username))
            {
                _logger.LogDebug("No registration found for backend {BackendId}", backend.Id);
                return AuthenticationState.NotRegistered;
            }

            if (!await _credentials.HasValidCredentialsAsync(backend.Id))
                return AuthenticationState.NotAuthenticated;

            // Local expiry alone cannot prove that a token is still valid. The backend may
            // have been reset or the screen user may have been deleted since the last run.
            try
            {
                await _apiClient.GetApiScreenmanagementBonjourAsync();
            }
            catch (ApiException ex) when (ex.StatusCode is 302 or 401 or 403)
            {
                _logger.LogInformation(
                    ex,
                    "Stored token was rejected by backend {BackendId}; login recovery required",
                    backend.Id
                );
                return AuthenticationState.NotAuthenticated;
            }
            catch (ApiException ex) when (ex.StatusCode == 404)
            {
                _logger.LogInformation(
                    ex,
                    "Screen registration no longer exists on backend {BackendId}",
                    backend.Id
                );
                await _credentials.DeleteCredentialsAsync(backend.Id);
                return AuthenticationState.NotRegistered;
            }
            catch (Exception ex)
            {
                // Connectivity failures must not cause a replacement screen to be created.
                // Let SignalR's normal retry policy handle an unavailable backend.
                _logger.LogWarning(
                    ex,
                    "Could not validate stored token for backend {BackendId}; keeping local authentication state",
                    backend.Id
                );
            }

            _logger.LogDebug("Valid credentials found for backend {BackendId}", backend.Id);
            return AuthenticationState.Authenticated;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking authentication state");
            return AuthenticationState.Failed;
        }
    }

    public async Task<RegisterResult> RegisterAsync(string? deviceName = null)
    {
        try
        {
            var backend = await _backendManager.GetCurrentBackendAsync();
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

            _logger.LogInformation("Registering screen with backend {BackendId}", backend.Id);

            // Register with backend
            var request = new RegisterScreenRequest
            {
                Username = username,
                Password = password,
                DeviceName = deviceName,
                ResolutionWidth = null,
                ResolutionHeight = null,
            };

            var response = await _apiClient.PostApiScreenmanagementRegisterAsync(request);

            _logger.LogInformation(
                "Registration successful. Screen identifier: {ScreenIdentifier}",
                response.ScreenIdentifier
            );

            await _credentials.SaveRegistrationAsync(backend.Id, username, password);

            return new RegisterResult(true, response.ScreenIdentifier, response.UserId, null);
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "Registration failed");
            return new RegisterResult(false, null, null, $"Registration failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during registration");
            return new RegisterResult(false, null, null, $"Unexpected error: {ex.Message}");
        }
    }

    public async Task<LoginResult> LoginAsync()
    {
        try
        {
            var backend = await _backendManager.GetCurrentBackendAsync();
            if (backend == null)
                return new LoginResult(
                    false,
                    null,
                    "No backend configured. Please select a backend first."
                );

            _logger.LogInformation(
                "Attempting login for backend {BackendId} - {BaseUrl}",
                backend.Id,
                backend.BaseUrl
            );

            var credential = await _credentials.GetCredentialsAsync(backend.Id);
            if (credential == null || string.IsNullOrEmpty(credential.Username))
                return new LoginResult(false, null, "No credentials found. Please register first.");

            var (response, errorMessage) = await AcquireAccessTokenAsync(backend.Id, credential);
            if (response == null)
                return new LoginResult(false, null, errorMessage ?? "Login failed.");

            await _credentials.SaveTokensAsync(
                backend.Id,
                response.AccessToken,
                response.RefreshToken,
                DateTime.UtcNow.AddSeconds(response.ExpiresIn)
            );

            await _hubService.ConnectAsync();
            _logger.LogInformation(
                "Login succeeded for backend {BackendId}: credentials saved, connected to hub",
                backend.Id
            );

            return new LoginResult(true, response.AccessToken, null);
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "Login failed");
            return new LoginResult(false, null, $"Login failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during login");
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

        if (response == null)
        {
            if (
                credential is
                {
                    Username: { Length: > 0 } storedUsername,
                    Password: { Length: > 0 } storedPassword,
                }
            )
                response = await TryLoginWithPasswordAsync(
                    storedUsername,
                    storedPassword,
                    backendId
                );
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
            _logger.LogWarning(
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
        await _credentials.DeleteCredentialsAsync(backendId);
        var registration = await RegisterAsync();
        if (!registration.Success)
            return (null, registration.ErrorMessage);

        var replacement = await _credentials.GetCredentialsAsync(backendId);
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
            _logger.LogDebug("Refreshing access token for backend {BackendId}", backendId);
            return await _apiClient.PostRefreshAsync(
                new RefreshRequest { RefreshToken = refreshToken }
            );
        }
        catch (ApiException ex) when (ex.StatusCode is 400 or 401)
        {
            _logger.LogInformation(
                ex,
                "Refresh token was rejected for backend {BackendId}; trying the stored password",
                backendId
            );
            return null;
        }
    }

    private Task<AccessTokenResponse> LoginWithPasswordAsync(string username, string password)
    {
        // The Identity API field is named Email but PasswordSignInAsync resolves UserName.
        var request = new LoginRequest { Email = username, Password = password };
        return _apiClient.PostLoginAsync(false, false, request);
    }

    public async Task<ScreenInfo?> GetScreenInfoAsync()
    {
        try
        {
            var backend = await _backendManager.GetCurrentBackendAsync();
            if (backend == null)
            {
                _logger.LogWarning("Cannot fetch screen info: No backend configured");
                return null;
            }

            var credential = await _credentials.GetCredentialsAsync(backend.Id);
            if (credential == null || string.IsNullOrEmpty(credential.AccessToken))
            {
                _logger.LogWarning(
                    "Cannot fetch screen info: Not authenticated for backend {BackendId}",
                    backend.Id
                );
                return null;
            }

            _logger.LogDebug("Fetching screen info for backend {BackendId}", backend.Id);

            var response = await _apiClient.GetApiScreenmanagementBonjourAsync();

            _logger.LogInformation(
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
            _logger.LogError(
                ex,
                "API error fetching screen info. Status: {StatusCode}",
                ex.StatusCode
            );
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching screen info");
            return null;
        }
    }

    public async Task LogoutAsync()
    {
        try
        {
            var backend = await _backendManager.GetCurrentBackendAsync();
            if (backend == null)
            {
                _logger.LogWarning("Cannot logout: No backend configured");
                return;
            }

            _logger.LogInformation("Logging out from backend {BackendId}", backend.Id);

            // Disconnect from SignalR Hub
            await _hubService.DisconnectAsync();
            _logger.LogDebug("Disconnected from SignalR hub");

            // Delete credentials from database
            await _credentials.DeleteCredentialsAsync(backend.Id);
            _logger.LogInformation("Credentials deleted for backend {BackendId}", backend.Id);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Logout failed for backend. See inner exception for details.",
                ex
            );
        }
    }

    public string? GetAccessToken() => _credentials.GetAccessToken();

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
