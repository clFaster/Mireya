using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Mireya.Client.Avalonia.Data;
using Mireya.Client.Avalonia.Services;

namespace Mireya.Client.Avalonia.ViewModels;

public partial class BackendSelectionViewModel : ViewModelBase
{
    private readonly IApiClientConfiguration _apiClientConfiguration;
    private readonly IBackendManager _backendManager;
    private readonly ILogger<BackendSelectionViewModel> _logger;
    private readonly Action<BackendInstance> _onBackendSelected;

    [ObservableProperty]
    private ObservableCollection<BackendInstance> _backends = new();

    [ObservableProperty]
    private bool _isStatusError;

    [ObservableProperty]
    private string _newBackendUrl = string.Empty;

    [ObservableProperty]
    private BackendInstance? _selectedBackend;

    [ObservableProperty]
    private string? _statusMessage;

    public BackendSelectionViewModel(
        IBackendManager backendManager,
        IApiClientConfiguration apiClientConfiguration,
        ILogger<BackendSelectionViewModel> logger,
        Action<BackendInstance> onBackendSelected
    )
    {
        _backendManager = backendManager;
        _apiClientConfiguration = apiClientConfiguration;
        _logger = logger;
        _onBackendSelected = onBackendSelected;

        _ = LoadBackendsAsync();
    }

    private async Task LoadBackendsAsync()
    {
        _logger.LogInformation("Loading backends...");

        try
        {
            var backends = await _backendManager.GetAllBackendsAsync();
            Backends = new ObservableCollection<BackendInstance>(backends);

            // Select the most recently used backend
            SelectedBackend =
                backends.FirstOrDefault(b => b.IsCurrentBackend) ?? backends.FirstOrDefault();

            _logger.LogInformation("Loaded {Count} backend(s)", backends.Count);

            if (backends.Count == 0)
            {
                StatusMessage = "No backends configured. Please add a new backend URL below.";
                IsStatusError = false;
            }
            else
            {
                StatusMessage = $"Found {backends.Count} backend(s). Select one or add a new one.";
                IsStatusError = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load backends");
            StatusMessage = $"Failed to load backends: {ex.Message}";
            IsStatusError = true;
        }
    }

    [RelayCommand]
    private async Task AddBackendAsync()
    {
        if (string.IsNullOrWhiteSpace(NewBackendUrl))
        {
            StatusMessage = "Please enter a backend URL.";
            IsStatusError = true;
            return;
        }

        if (!IsValidUrl(NewBackendUrl))
        {
            StatusMessage = "Invalid URL format. Please enter a valid HTTP or HTTPS URL.";
            IsStatusError = true;
            return;
        }

        try
        {
            _logger.LogInformation("Adding new backend: {Url}", NewBackendUrl);

            var backend = await _backendManager.GetOrCreateBackendAsync(NewBackendUrl);

            // Reload the list
            await LoadBackendsAsync();

            // Select the newly added backend
            SelectedBackend = Backends.FirstOrDefault(b => b.Id == backend.Id);

            StatusMessage = $"✓ Backend added: {NewBackendUrl}";
            IsStatusError = false;
            NewBackendUrl = string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add backend");
            StatusMessage = $"Failed to add backend: {ex.Message}";
            IsStatusError = true;
        }
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (SelectedBackend == null)
        {
            StatusMessage = "Please select a backend first.";
            IsStatusError = true;
            return;
        }

        try
        {
            _logger.LogInformation(
                "Connecting to backend: {BackendId} - {Url}",
                SelectedBackend.Id,
                SelectedBackend.BaseUrl
            );

            // Set as current backend
            await _backendManager.SetCurrentBackendAsync(SelectedBackend.Id);

            // Update API client configuration
            await _apiClientConfiguration.UpdateBaseUrlAsync(SelectedBackend.BaseUrl);

            StatusMessage = $"✓ Connected to {SelectedBackend.BaseUrl}";
            IsStatusError = false;

            _logger.LogInformation("Backend connection successful, notifying parent...");

            // Notify parent to switch view
            _onBackendSelected(SelectedBackend);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to backend");
            StatusMessage = $"Failed to connect: {ex.Message}";
            IsStatusError = true;
        }
    }

    [RelayCommand]
    private async Task DeleteBackendAsync(BackendInstance? backend)
    {
        if (backend == null)
            return;

        _logger.LogInformation(
            "Deleting backend: {BackendId} - {Url}",
            backend.Id,
            backend.BaseUrl
        );

        // TODO: Implement delete logic (remove from database, clean up assets, etc.)
        StatusMessage = "Delete functionality not yet implemented.";
        IsStatusError = false;
    }

    private bool IsValidUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
            && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
    }
}
