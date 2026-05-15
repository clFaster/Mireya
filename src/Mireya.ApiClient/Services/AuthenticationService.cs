using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Mireya.ApiClient.Generated;
using Mireya.ApiClient.Models;

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
            var backend = await _backendManager.GetCurrentBackendAsync();
            if (backend == null)
            {
                _logger.LogDebug("No backend configured");
                return AuthenticationState.NotRegistered;
            }

            // Check if we have valid credentials for current backend
            var hasValidCredentials = await _credentials.HasValidCredentialsAsync(backend.Id);
            if (!hasValidCredentials)
            {
                _logger.LogDebug("No valid credentials for backend {BackendId}", backend.Id);

                // Check legacy credential storage for migration
                if (await _credentials.HasLegacyCredentialsAsync())
                {
                    _logger.LogInformation("Found legacy credentials, attempting migration");
                    return AuthenticationState.NotAuthenticated; // Will try to login and migrate
                }

                return AuthenticationState.NotRegistered;
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

            // Store credentials temporarily in legacy storage (for backward compatibility)
            var credentials = new Credentials(username, password);
            await _credentials.SaveLegacyCredentialsAsync(credentials);

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
                return new LoginResult(false, null, "No backend configured. Please select a backend first.");

            _logger.LogInformation("Attempting login for backend {BackendId} - {BaseUrl}", backend.Id, backend.BaseUrl);

            var (loginIdentity, password) = await ResolveLoginCredentialsAsync(backend.Id);
            if (loginIdentity == null)
                return new LoginResult(false, null, "No credentials found. Please register first.");

            // The Identity API POST /login endpoint names the field "Email" but internally
            // passes it to PasswordSignInAsync which looks up by UserName, not Email.
            // Send the raw username (e.g. "screen-{guid}"), NOT the email form.
            var loginRequest = new LoginRequest
            {
                Email = loginIdentity,
                Password = password!,
            };

            var response = await _apiClient.PostLoginAsync(false, false, loginRequest);

            await _credentials.SaveCredentialsAsync(
                backend.Id,
                loginRequest.Email,
                response.AccessToken,
                response.RefreshToken,
                DateTime.UtcNow.AddSeconds(response.ExpiresIn)
            );

            await _hubService.ConnectAsync();
            _logger.LogInformation("Login succeeded for backend {BackendId}: credentials saved, connected to hub", backend.Id);

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

    private async Task<(string? loginIdentity, string? password)> ResolveLoginCredentialsAsync(Guid backendId)
    {
        var credential = await _credentials.GetCredentialsAsync(backendId);
        var legacyCredentials = await _credentials.GetLegacyCredentialsAsync();

        if (credential == null && legacyCredentials == null)
        {
            _logger.LogWarning("No credentials found for backend {BackendId}", backendId);
            return (null, null);
        }

        if (credential != null)
        {
            _logger.LogInformation("Using stored credentials for backend {BackendId}", backendId);
            var password = legacyCredentials?.Password ?? "dummy";
            return (credential.Username, password);
        }

        _logger.LogInformation("Using legacy credentials for migration");
        return (legacyCredentials!.Username, legacyCredentials.Password);
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
            throw new InvalidOperationException($"Logout failed for backend. See inner exception for details.", ex);
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
