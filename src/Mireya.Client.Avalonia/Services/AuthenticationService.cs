using System;
using System.Threading.Tasks;
using Mireya.ApiClient.Generated;
using Mireya.ApiClient.Models;

namespace Mireya.Client.Avalonia.Services;

/// <summary>
/// Implementation of authentication service for screen clients
/// </summary>
public class AuthenticationService : IAuthenticationService
{
    private readonly IMireyaApiClient _apiClient;
    private readonly ICredentialStorage _credentialStorage;
    private readonly IAccessTokenProvider _tokenProvider;
    private string? _accessToken;

    public AuthenticationService(
        IMireyaApiClient apiClient,
        ICredentialStorage credentialStorage,
        IAccessTokenProvider tokenProvider)
    {
        _apiClient = apiClient;
        _credentialStorage = credentialStorage;
        _tokenProvider = tokenProvider;
    }

    public async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            // Check if credentials exist
            if (!await _credentialStorage.HasCredentialsAsync())
            {
                return AuthenticationState.NotRegistered;
            }

            // Check if we have a valid access token
            if (!string.IsNullOrEmpty(_accessToken))
            {
                return AuthenticationState.Authenticated;
            }

            return AuthenticationState.NotAuthenticated;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AuthenticationService] Error checking auth state: {ex.Message}");
            return AuthenticationState.Failed;
        }
    }

    public async Task<RegisterResult> RegisterAsync(string? deviceName = null)
    {
        try
        {
            // Generate credentials on the client
            var username = GenerateUsername();
            var password = GeneratePassword();
            
            // Store credentials before registration
            var credentials = new Credentials(username, password);
            await _credentialStorage.SaveCredentialsAsync(credentials);

            // Register with backend
            var request = new RegisterScreenRequest
            {
                Username = username,
                Password = password,
                DeviceName = deviceName,
                ResolutionWidth = null, // Could be detected from screen
                ResolutionHeight = null
            };

            var response = await _apiClient.ScreenManagement_RegisterScreenAsync(request);

            return new RegisterResult(
                Success: true,
                ScreenIdentifier: response.ScreenIdentifier,
                UserId: response.UserId,
                ErrorMessage: null);
        }
        catch (ApiException ex)
        {
            Console.WriteLine($"[AuthenticationService] Registration failed: {ex.Message}");
            
            // Clean up stored credentials on failure
            await _credentialStorage.DeleteCredentialsAsync();
            
            return new RegisterResult(
                Success: false,
                ScreenIdentifier: null,
                UserId: null,
                ErrorMessage: $"Registration failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AuthenticationService] Unexpected error during registration: {ex.Message}");
            
            await _credentialStorage.DeleteCredentialsAsync();
            
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
            // Get stored credentials
            var credentials = await _credentialStorage.GetCredentialsAsync();
            if (credentials == null)
            {
                return new LoginResult(
                    Success: false,
                    AccessToken: null,
                    ErrorMessage: "No credentials found. Please register first.");
            }

            // Login with backend (useCookies=false for JWT tokens)
            var loginRequest = new LoginRequest
            {
                Email = credentials.Username,
                Password = credentials.Password
            };

            var response = await _apiClient.PostLoginAsync(
                useCookies: false,
                useSessionCookies: false,
                login: loginRequest);

            // Store tokens
            _accessToken = response.AccessToken;
            _tokenProvider.SetAccessToken(response.AccessToken);
            // Note: RefreshToken is available in response.RefreshToken if needed for token refresh

            return new LoginResult(
                Success: true,
                AccessToken: response.AccessToken,
                ErrorMessage: null);
        }
        catch (ApiException ex)
        {
            Console.WriteLine($"[AuthenticationService] Login failed: {ex.Message}");
            
            return new LoginResult(
                Success: false,
                AccessToken: null,
                ErrorMessage: $"Login failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AuthenticationService] Unexpected error during login: {ex.Message}");
            
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
            if (string.IsNullOrEmpty(_accessToken))
            {
                Console.WriteLine("[AuthenticationService] Cannot fetch screen info: Not authenticated");
                return null;
            }

            Console.WriteLine($"[AuthenticationService] Fetching screen info with token: {_accessToken?.Substring(0, Math.Min(20, _accessToken.Length))}...");
            
            var response = await _apiClient.ScreenManagement_BonjourAsync();

            Console.WriteLine($"[AuthenticationService] Successfully fetched screen info: {response.ScreenIdentifier}");
            
            return new ScreenInfo(
                ScreenIdentifier: response.ScreenIdentifier,
                ScreenName: response.ScreenName,
                Description: response.Description,
                ApprovalStatus: response.ApprovalStatus.ToString());
        }
        catch (ApiException ex)
        {
            Console.WriteLine($"[AuthenticationService] API error fetching screen info:");
            Console.WriteLine($"  Status Code: {ex.StatusCode}");
            Console.WriteLine($"  Message: {ex.Message}");
            Console.WriteLine($"  Response: {ex.Response}");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AuthenticationService] Unexpected error fetching screen info:");
            Console.WriteLine($"  Type: {ex.GetType().Name}");
            Console.WriteLine($"  Message: {ex.Message}");
            Console.WriteLine($"  Stack: {ex.StackTrace}");
            return null;
        }
    }

    public async Task LogoutAsync()
    {
        try
        {
            // Clear tokens
            _accessToken = null;
            _tokenProvider.SetAccessToken(null);
            
            // Delete stored credentials
            await _credentialStorage.DeleteCredentialsAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AuthenticationService] Error during logout: {ex.Message}");
            throw;
        }
    }

    public string? GetAccessToken()
    {
        return _accessToken;
    }

    /// <summary>
    /// Generate a unique username using GUID
    /// </summary>
    private static string GenerateUsername()
    {
        return $"screen-{Guid.NewGuid():N}";
    }

    /// <summary>
    /// Generate a secure random password
    /// </summary>
    private static string GeneratePassword()
    {
        // Generate a 32-character password using Guid for simplicity
        // For production, consider using a cryptographically secure random generator
        return Convert.ToBase64String(Guid.NewGuid().ToByteArray()) + 
               Convert.ToBase64String(Guid.NewGuid().ToByteArray());
    }
}
