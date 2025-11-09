using Microsoft.Extensions.Logging;
using Mireya.ApiClient.Generated;
using Mireya.ApiClient.Models;

namespace Mireya.ApiClient.Services.Authentication;

/// <summary>
/// Implementation of authentication service for screen clients
/// </summary>
public class AuthenticationService : IAuthenticationService
{
    private readonly IMireyaApiClient _apiClient;
    private readonly ICredentialStorage _credentialStorage;
    private readonly ILogger<AuthenticationService> _logger;
    private string? _accessToken;

    public AuthenticationService(
        IMireyaApiClient apiClient,
        ICredentialStorage credentialStorage,
        ILogger<AuthenticationService> logger)
    {
        _apiClient = apiClient;
        _credentialStorage = credentialStorage;
        _logger = logger;
    }

    public async Task<AuthenticationState> GetStateAsync()
    {
        try
        {
            var credentials = await _credentialStorage.LoadAsync();
            if (credentials == null)
            {
                return AuthenticationState.NotRegistered;
            }

            // If we have a token, we're authenticated
            if (!string.IsNullOrEmpty(_accessToken))
            {
                return AuthenticationState.Authenticated;
            }

            // Have credentials but not logged in
            return AuthenticationState.Registered;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking authentication state");
            return AuthenticationState.NotRegistered;
        }
    }

    public async Task<RegisterResult> RegisterAsync(string? deviceName = null, int? resolutionWidth = null, int? resolutionHeight = null)
    {
        try
        {
            // Check if already registered
            var existing = await _credentialStorage.LoadAsync();
            if (existing != null)
            {
                _logger.LogWarning("Credentials already exist. Use LoginAsync() or ResetAsync() first.");
                return new RegisterResult 
                { 
                    Success = false, 
                    ErrorMessage = "Already registered. Reset credentials first." 
                };
            }

            // Generate new credentials
            var credentials = CredentialGenerator.Generate();
            _logger.LogInformation("Generated new credentials for screen registration");

            // Register with API
            var registerRequest = new RegisterScreenRequest
            {
                Username = credentials.Username,
                Password = credentials.Password,
                DeviceName = deviceName,
                ResolutionWidth = resolutionWidth,
                ResolutionHeight = resolutionHeight
            };

            var response = await _apiClient.ScreenManagement_RegisterScreenAsync(registerRequest);
            _logger.LogInformation("Screen registered successfully. ScreenIdentifier: {ScreenIdentifier}", response.ScreenIdentifier);

            // Store credentials securely
            await _credentialStorage.SaveAsync(credentials);

            // Automatically login after registration
            var loginResult = await LoginAsync();
            if (!loginResult.Success)
            {
                return new RegisterResult
                {
                    Success = false,
                    ScreenIdentifier = response.ScreenIdentifier,
                    ErrorMessage = $"Registration succeeded but login failed: {loginResult.ErrorMessage}"
                };
            }

            return new RegisterResult
            {
                Success = true,
                ScreenIdentifier = response.ScreenIdentifier,
                UserId = response.UserId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registration failed");
            return new RegisterResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<LoginResult> LoginAsync()
    {
        try
        {
            var credentials = await _credentialStorage.LoadAsync();
            if (credentials == null)
            {
                _logger.LogWarning("No credentials found. Register first.");
                return new LoginResult
                {
                    Success = false,
                    ErrorMessage = "No credentials found. Please register first."
                };
            }

            // TODO: Call Identity API login endpoint
            // The generated client might not have the Identity endpoints
            // We'll need to add a custom HttpClient call here or extend the generated client
            
            _logger.LogWarning("Login implementation pending - need to call Identity API /login endpoint");
            
            // For now, return a placeholder response
            return new LoginResult
            {
                Success = false,
                ErrorMessage = "Login endpoint integration pending"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed");
            return new LoginResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<BonjourResponse> GetBonjourDataAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(_accessToken))
            {
                throw new InvalidOperationException("Not authenticated. Call LoginAsync() first.");
            }

            // Call the Bonjour endpoint
            var response = await _apiClient.ScreenManagement_BonjourAsync();
            _logger.LogInformation("Retrieved screen data from Bonjour endpoint");
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Bonjour data");
            throw;
        }
    }

    public Task LogoutAsync()
    {
        _accessToken = null;
        _logger.LogInformation("Logged out (cleared access token)");
        return Task.CompletedTask;
    }

    public async Task ResetAsync()
    {
        await _credentialStorage.DeleteAsync();
        _accessToken = null;
        _logger.LogInformation("Reset completed (cleared credentials and token)");
    }

    public string? GetAccessToken()
    {
        return _accessToken;
    }
}
