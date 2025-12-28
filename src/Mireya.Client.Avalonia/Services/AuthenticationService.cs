using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Mireya.ApiClient.Generated;
using Mireya.ApiClient.Models;
using Mireya.ApiClient.Services;

namespace Mireya.Client.Avalonia.Services;

/// <summary>
/// Implementation of authentication service for screen clients
/// Uses database-backed credential storage per backend
/// </summary>
public class AuthenticationService : IAuthenticationService
{
    private readonly IMireyaApiClient _apiClient;
    private readonly ICredentialStorage _legacyCredentialStorage;
    private readonly ICredentialManager _credentialManager;
    private readonly IBackendManager _backendManager;
    private readonly IAccessTokenProvider _tokenProvider;
    private readonly IScreenHubService _hubService;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(
        IMireyaApiClient apiClient,
        ICredentialStorage legacyCredentialStorage,
        ICredentialManager credentialManager,
        IBackendManager backendManager,
        IAccessTokenProvider tokenProvider,
        IScreenHubService hubService,
        ILogger<AuthenticationService> logger)
    {
        _apiClient = apiClient;
        _legacyCredentialStorage = legacyCredentialStorage;
        _credentialManager = credentialManager;
        _backendManager = backendManager;
        _tokenProvider = tokenProvider;
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
            var hasValidCredentials = await _credentialManager.HasValidCredentialsAsync(backend.Id);
            if (!hasValidCredentials)
            {
                _logger.LogDebug("No valid credentials for backend {BackendId}", backend.Id);
                
                // Check legacy credential storage for migration
                if (await _legacyCredentialStorage.HasCredentialsAsync())
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
            {
                return new RegisterResult(
                    Success: false,
                    ScreenIdentifier: null,
                    UserId: null,
                    ErrorMessage: "No backend configured. Please select a backend first.");
            }

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
                ResolutionHeight = null
            };

            var response = await _apiClient.ScreenManagement_RegisterScreenAsync(request);

            _logger.LogInformation("Registration successful. Screen identifier: {ScreenIdentifier}", 
                response.ScreenIdentifier);

            // Store credentials temporarily in legacy storage (for backward compatibility)
            var credentials = new Credentials(username, password);
            await _legacyCredentialStorage.SaveCredentialsAsync(credentials);

            return new RegisterResult(
                Success: true,
                ScreenIdentifier: response.ScreenIdentifier,
                UserId: response.UserId,
                ErrorMessage: null);
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "Registration failed");
            return new RegisterResult(
                Success: false,
                ScreenIdentifier: null,
                UserId: null,
                ErrorMessage: $"Registration failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during registration");
            return new RegisterResult(
                Success: false,
                ScreenIdentifier: null,
                UserId: null,
                ErrorMessage: $"Unexpected error: {ex.Message}");
        }
    }

    public async Task<LoginResult> LoginAsync()
    {
        try
        {
            var backend = await _backendManager.GetCurrentBackendAsync();
            if (backend == null)
            {
                return new LoginResult(
                    Success: false,
                    AccessToken: null,
                    ErrorMessage: "No backend configured. Please select a backend first.");
            }

            _logger.LogInformation("Attempting login for backend {BackendId} - {BaseUrl}", 
                backend.Id, backend.BaseUrl);

            // Try to get credentials from new storage first
            var credential = await _credentialManager.GetCredentialsAsync(backend.Id);
            Credentials? legacyCredentials = null;
            
            if (credential == null)
            {
                // Try legacy storage for migration
                legacyCredentials = await _legacyCredentialStorage.GetCredentialsAsync();
                if (legacyCredentials == null)
                {
                    _logger.LogWarning("No credentials found for backend {BackendId}", backend.Id);
                    return new LoginResult(
                        Success: false,
                        AccessToken: null,
                        ErrorMessage: "No credentials found. Please register first.");
                }
                
                _logger.LogInformation("Using legacy credentials for migration");
            }

            // Login with backend (useCookies=false for JWT tokens)
            var loginRequest = new LoginRequest
            {
                Email = credential?.Username ?? legacyCredentials!.Username,
                Password = "dummy" // We don't store passwords, only use for initial registration
            };

            // If we have credentials from new storage but need password, try legacy
            if (credential != null && legacyCredentials == null)
            {
                legacyCredentials = await _legacyCredentialStorage.GetCredentialsAsync();
            }

            if (legacyCredentials != null)
            {
                loginRequest.Password = legacyCredentials.Password;
            }

            var response = await _apiClient.PostLoginAsync(
                useCookies: false,
                useSessionCookies: false,
                login: loginRequest);

            _logger.LogInformation("Login successful for backend {BackendId}", backend.Id);

            // Save credentials to new database storage
            await _credentialManager.SaveCredentialsAsync(
                backend.Id,
                loginRequest.Email,
                response.AccessToken,
                response.RefreshToken,
                DateTime.UtcNow.AddSeconds(response.ExpiresIn));

            _logger.LogInformation("Credentials saved to database for backend {BackendId}", backend.Id);

            // Connect to SignalR Hub
            await _hubService.ConnectAsync();
            _logger.LogInformation("Connected to SignalR hub");

            return new LoginResult(
                Success: true,
                AccessToken: response.AccessToken,
                ErrorMessage: null);
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "Login failed");
            return new LoginResult(
                Success: false,
                AccessToken: null,
                ErrorMessage: $"Login failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during login");
            return new LoginResult(
                Success: false,
                AccessToken: null,
                ErrorMessage: $"Unexpected error: {ex.Message}");
        }
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

            var credential = await _credentialManager.GetCredentialsAsync(backend.Id);
            if (credential == null || string.IsNullOrEmpty(credential.AccessToken))
            {
                _logger.LogWarning("Cannot fetch screen info: Not authenticated for backend {BackendId}", backend.Id);
                return null;
            }

            _logger.LogDebug("Fetching screen info for backend {BackendId}", backend.Id);
            
            var response = await _apiClient.ScreenManagement_BonjourAsync();

            _logger.LogInformation("Successfully fetched screen info: {ScreenIdentifier}", 
                response.ScreenIdentifier);
            
            return new ScreenInfo(
                ScreenIdentifier: response.ScreenIdentifier,
                ScreenName: response.ScreenName,
                Description: response.Description,
                ApprovalStatus: response.ApprovalStatus.ToString());
        }
        catch (ApiException ex)
        {
            _logger.LogError(ex, "API error fetching screen info. Status: {StatusCode}", ex.StatusCode);
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
            await _credentialManager.DeleteCredentialsAsync(backend.Id);
            _logger.LogInformation("Credentials deleted for backend {BackendId}", backend.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            throw;
        }
    }

    public string? GetAccessToken()
    {
        return _tokenProvider.GetAccessToken();
    }

    private static string GenerateUsername()
    {
        return $"screen-{Guid.NewGuid():N}";
    }

    private static string GeneratePassword()
    {
        return Convert.ToBase64String(Guid.NewGuid().ToByteArray()) + 
               Convert.ToBase64String(Guid.NewGuid().ToByteArray());
    }
}

