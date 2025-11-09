using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mireya.Client.Avalonia.Services;

namespace Mireya.Client.Avalonia.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IAuthenticationService _authenticationService;
    
    [ObservableProperty]
    private string? _backendUrl;
    
    [ObservableProperty]
    private string? _statusMessage;
    
    [ObservableProperty]
    private bool _isStatusError;
    
    [ObservableProperty]
    private string _authStatus = "Not checked";
    
    [ObservableProperty]
    private bool _isAuthenticated;

    public MainWindowViewModel(
        ISettingsService settingsService,
        IAuthenticationService authenticationService)
    {
        _settingsService = settingsService;
        _authenticationService = authenticationService;
        _ = InitializeAsync();
    }
    
    /// <summary>
    /// Load settings and check authentication status
    /// </summary>
    private async Task InitializeAsync()
    {
        // Load backend URL
        BackendUrl = await _settingsService.GetBackendUrlAsync();
        if (string.IsNullOrEmpty(BackendUrl))
        {
            StatusMessage = "No backend URL configured. Please enter one below.";
            IsStatusError = false;
        }
        else
        {
            StatusMessage = $"Loaded saved URL: {BackendUrl}";
            IsStatusError = false;
        }
        
        // Check authentication state
        await CheckAuthenticationAsync();
    }
    
    private async Task CheckAuthenticationAsync()
    {
        var state = await _authenticationService.GetAuthenticationStateAsync();
        
        AuthStatus = state switch
        {
            AuthenticationState.NotRegistered => "Not registered - need to register",
            AuthenticationState.NotAuthenticated => "Credentials found - need to login",
            AuthenticationState.Authenticated => "Authenticated ✓",
            AuthenticationState.Failed => "Authentication failed",
            _ => "Unknown"
        };
        
        IsAuthenticated = state == AuthenticationState.Authenticated;
    }

    [RelayCommand]
    private async Task RegisterAsync()
    {
        if (string.IsNullOrWhiteSpace(BackendUrl))
        {
            StatusMessage = "Please set a backend URL first.";
            IsStatusError = true;
            return;
        }

        StatusMessage = "Registering...";
        IsStatusError = false;
        
        var result = await _authenticationService.RegisterAsync();
        
        if (result.Success)
        {
            StatusMessage = $"✓ Registered successfully! Screen ID: {result.ScreenIdentifier}";
            IsStatusError = false;
            await CheckAuthenticationAsync();
        }
        else
        {
            StatusMessage = $"Registration failed: {result.ErrorMessage}";
            IsStatusError = true;
        }
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(BackendUrl))
        {
            StatusMessage = "Please set a backend URL first.";
            IsStatusError = true;
            return;
        }

        StatusMessage = "Logging in...";
        IsStatusError = false;
        
        var result = await _authenticationService.LoginAsync();
        
        if (result.Success)
        {
            StatusMessage = "✓ Login successful!";
            IsStatusError = false;
            await CheckAuthenticationAsync();
        }
        else
        {
            StatusMessage = $"Login failed: {result.ErrorMessage}";
            IsStatusError = true;
        }
    }

    [RelayCommand]
    private async Task FetchScreenInfoAsync()
    {
        StatusMessage = "Fetching screen info...";
        IsStatusError = false;
        
        var screenInfo = await _authenticationService.GetScreenInfoAsync();
        
        if (screenInfo != null)
        {
            StatusMessage = $"✓ Screen: {screenInfo.ScreenName} ({screenInfo.ScreenIdentifier})\n" +
                          $"Status: {screenInfo.ApprovalStatus}\n" +
                          $"Description: {screenInfo.Description ?? "N/A"}";
            IsStatusError = false;
        }
        else
        {
            StatusMessage = "Failed to fetch screen info. Are you authenticated?";
            IsStatusError = true;
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        try
        {
            await _authenticationService.LogoutAsync();
            StatusMessage = "✓ Logged out successfully";
            IsStatusError = false;
            await CheckAuthenticationAsync();
        }
        catch
        {
            StatusMessage = "Failed to logout";
            IsStatusError = true;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(BackendUrl))
        {
            StatusMessage = "Please enter a backend URL.";
            IsStatusError = true;
            return;
        }

        if (!_settingsService.IsValidUrl(BackendUrl))
        {
            StatusMessage = "Invalid URL format. Please enter a valid HTTP or HTTPS URL.";
            IsStatusError = true;
            return;
        }

        try
        {
            await _settingsService.SaveBackendUrlAsync(BackendUrl);
            StatusMessage = $"✓ Backend URL saved successfully: {BackendUrl}";
            IsStatusError = false;
        }
        catch
        {
            StatusMessage = "Failed to save backend URL. Please try again.";
            IsStatusError = true;
        }
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        // Reload from settings to reset any unsaved changes
        var savedUrl = await _settingsService.GetBackendUrlAsync();
        BackendUrl = savedUrl;
        StatusMessage = "Changes cancelled. Restored previous value.";
        IsStatusError = false;
    }
}
