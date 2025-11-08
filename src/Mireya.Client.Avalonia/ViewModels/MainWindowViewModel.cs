using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mireya.Client.Avalonia.Services;

namespace Mireya.Client.Avalonia.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    
    [ObservableProperty]
    private string? _backendUrl;
    
    [ObservableProperty]
    private string? _statusMessage;
    
    [ObservableProperty]
    private bool _isStatusError;

    public MainWindowViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        _ = LoadSettingsAsync();
    }
    
    /// <summary>
    /// Load settings when view model is created
    /// </summary>
    private async Task LoadSettingsAsync()
    {
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
