using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Mireya.ApiClient.Data;
using Mireya.ApiClient.Services;
using Mireya.Client.Avalonia.Services;

namespace Mireya.Client.Avalonia.ViewModels;

public partial class BackendSelectionViewModel : ViewModelBase
{
    private readonly IApiClientConfiguration _apiClientConfiguration;
    private readonly IBackendManager _backendManager;
    private readonly ILogger<BackendSelectionViewModel> _logger;
    private readonly Action<BackendInstance> _onBackendSelected;
    private readonly AppSettings _appSettings;
    private CancellationTokenSource? _statusCts;

    [ObservableProperty]
    private ObservableCollection<BackendItemViewModel> _backends = [];

    [ObservableProperty]
    private bool _isStatusError;

    /// <summary>True while the add-server flow is running a Mireya server validation.</summary>
    [ObservableProperty]
    private bool _isVerifyingServer;

    /// <summary>Label shown on the Add Server button; changes while verification is in progress.</summary>
    public string AddButtonLabel => IsVerifyingServer ? "Checking…" : "Add Server";

    partial void OnIsVerifyingServerChanged(bool value) =>
        OnPropertyChanged(nameof(AddButtonLabel));

    [ObservableProperty]
    private string _newBackendUrl = string.Empty;

    [ObservableProperty]
    private BackendItemViewModel? _selectedBackend;

    [ObservableProperty]
    private string? _statusMessage;

    // ──────────────────────────────────────────────────────────────
    // Display settings (mirrored from AppSettings for two-way binding)
    // ──────────────────────────────────────────────────────────────

    [ObservableProperty]
    private bool _fullscreen;

    [ObservableProperty]
    private bool _autoStart;

    public BackendSelectionViewModel(
        IBackendManager backendManager,
        IApiClientConfiguration apiClientConfiguration,
        ILogger<BackendSelectionViewModel> logger,
        AppSettings appSettings,
        Action<BackendInstance> onBackendSelected
    )
    {
        _backendManager = backendManager;
        _apiClientConfiguration = apiClientConfiguration;
        _logger = logger;
        _appSettings = appSettings;
        _onBackendSelected = onBackendSelected;

        // Mirror current settings into the observable properties
        _fullscreen = appSettings.Fullscreen;
        _autoStart = appSettings.AutoStart;

        _ = LoadBackendsAsync();
    }

    // ──────────────────────────────────────────────────────────────
    // Selection change — subscribe to IsOnline so CanConnect updates
    // ──────────────────────────────────────────────────────────────

    partial void OnSelectedBackendChanged(
        BackendItemViewModel? oldValue,
        BackendItemViewModel? newValue
    )
    {
        if (oldValue != null)
            oldValue.PropertyChanged -= OnSelectedItemPropertyChanged;

        if (newValue != null)
            newValue.PropertyChanged += OnSelectedItemPropertyChanged;

        ConnectCommand.NotifyCanExecuteChanged();
    }

    private void OnSelectedItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (
            e.PropertyName
            is nameof(BackendItemViewModel.IsOnline)
                or nameof(BackendItemViewModel.IsCheckingOnline)
        )
            ConnectCommand.NotifyCanExecuteChanged();
    }

    // ──────────────────────────────────────────────────────────────
    // Load
    // ──────────────────────────────────────────────────────────────

    private async Task LoadBackendsAsync()
    {
        _logger.LogInformation("Loading backends...");

        try
        {
            var backends = await _backendManager.GetAllBackendsAsync();
            var items = backends
                .Select(b => new BackendItemViewModel(b, DoDeleteItemAsync))
                .ToList();
            Backends = new ObservableCollection<BackendItemViewModel>(items);

            // Prefer the last-used backend; fall back to the first one
            SelectedBackend =
                items.FirstOrDefault(b => b.Instance.IsCurrentBackend) ?? items.FirstOrDefault();

            _logger.LogInformation("Loaded {Count} backend(s)", backends.Count);

            if (backends.Count == 0)
                SetStatus(
                    "No servers configured yet. Add a server URL below.",
                    isError: false,
                    autoHide: false
                );

            // Kick off background online checks for every known server
            foreach (var item in items)
                _ = CheckOnlineStatusAsync(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load backends");
            SetStatus($"Failed to load backends: {ex.Message}", isError: true);
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Online status (non-blocking background check per server)
    // ──────────────────────────────────────────────────────────────

    private static async Task CheckOnlineStatusAsync(BackendItemViewModel item)
    {
        item.IsCheckingOnline = true;
        item.IsOnline = false;

        try
        {
            item.IsOnline = await VerifyMireyaServerAsync(item.Instance.BaseUrl);
        }
        catch
        {
            item.IsOnline = false;
        }
        finally
        {
            item.IsCheckingOnline = false;
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Save display settings
    // ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        try
        {
            _appSettings.Fullscreen = Fullscreen;
            _appSettings.AutoStart = AutoStart;
            await _appSettings.SaveAsync();
            _logger.LogInformation(
                "Settings saved — Fullscreen={Fullscreen}, AutoStart={AutoStart}",
                Fullscreen,
                AutoStart
            );
            // Apply fullscreen immediately — no restart required
            _appSettings.ApplyFullscreen?.Invoke(Fullscreen);
            SetStatus("Settings saved.", isError: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
            SetStatus($"Failed to save settings: {ex.Message}", isError: true);
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Add server (with Mireya verification)
    // ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task AddBackendAsync()
    {
        if (string.IsNullOrWhiteSpace(NewBackendUrl))
        {
            SetStatus("Please enter a server URL.", isError: true);
            return;
        }

        if (!IsValidUrl(NewBackendUrl))
        {
            SetStatus("Invalid URL format. Please enter a valid HTTP or HTTPS URL.", isError: true);
            return;
        }

        IsVerifyingServer = true;
        SetStatus("Verifying Mireya server…", isError: false, autoHide: false);

        try
        {
            var isMireya = await VerifyMireyaServerAsync(NewBackendUrl);
            if (!isMireya)
            {
                SetStatus(
                    "No Mireya server found at this URL. Please check the address and try again.",
                    isError: true
                );
                return;
            }

            _logger.LogInformation("Adding new backend: {Url}", NewBackendUrl);
            var backend = await _backendManager.GetOrCreateBackendAsync(NewBackendUrl);

            var addedUrl = NewBackendUrl;
            NewBackendUrl = string.Empty;

            await LoadBackendsAsync();
            SelectedBackend = Backends.FirstOrDefault(b => b.Instance.Id == backend.Id);

            SetStatus($"Server added: {addedUrl}", isError: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add backend");
            SetStatus($"Failed to add server: {ex.Message}", isError: true);
        }
        finally
        {
            IsVerifyingServer = false;
        }
    }

    /// <summary>
    /// Probes the given URL to confirm a Mireya API is running there.
    /// Primary signal: GET /api/info returns an application identifier of "Mireya".
    /// Fallback (older servers without /api/info): GET /api/screenmanagement/bonjour
    /// returns HTTP 401 with a Bearer challenge.
    /// </summary>
    private static async Task<bool> VerifyMireyaServerAsync(string baseUrl)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            client.DefaultRequestHeaders.Add("User-Agent", "Mireya-Client/1.0");

            var root = baseUrl.TrimEnd('/');

            // Primary: dedicated identity endpoint
            try
            {
                var infoResponse = await client.GetAsync($"{root}/api/info");
                if (infoResponse.StatusCode == HttpStatusCode.OK)
                {
                    await using var stream = await infoResponse.Content.ReadAsStreamAsync();
                    using var doc = await JsonDocument.ParseAsync(stream);
                    if (
                        doc.RootElement.TryGetProperty("application", out var appName)
                        && string.Equals(
                            appName.GetString(),
                            "Mireya",
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        return true;
                    }
                }
            }
            catch (JsonException)
            {
                // Not a Mireya server (returned non-JSON); fall through to the legacy probe.
            }

            // Fallback: legacy Bearer-challenge probe for older backends
            var response = await client.GetAsync($"{root}/api/screenmanagement/bonjour");
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                var wwwAuth = response.Headers.WwwAuthenticate.ToString();
                return wwwAuth.Contains("Bearer", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Connect — only allowed when the selected server is online
    // ──────────────────────────────────────────────────────────────

    private bool CanConnect() => SelectedBackend is { IsOnline: true };

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        if (SelectedBackend == null)
            return;

        try
        {
            var instance = SelectedBackend.Instance;
            _logger.LogInformation(
                "Connecting to backend: {BackendId} - {Url}",
                instance.Id,
                instance.BaseUrl
            );

            await _backendManager.SetCurrentBackendAsync(instance.Id);
            await _apiClientConfiguration.UpdateBaseUrlAsync(instance.BaseUrl);

            SetStatus($"Connected to {instance.BaseUrl}", isError: false);

            _logger.LogInformation("Backend connection successful, notifying parent…");
            _onBackendSelected(instance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to backend");
            SetStatus($"Failed to connect: {ex.Message}", isError: true);
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Delete — called from per-item DeleteCommand in BackendItemViewModel
    // ──────────────────────────────────────────────────────────────

    private async Task DoDeleteItemAsync(BackendItemViewModel item)
    {
        var backend = item.Instance;
        _logger.LogInformation(
            "Deleting backend: {BackendId} - {Url}",
            backend.Id,
            backend.BaseUrl
        );

        await _backendManager.DeleteBackendAsync(backend.Id);
        Backends.Remove(item);

        if (SelectedBackend?.Instance.Id == backend.Id)
            SelectedBackend = null;

        if (Backends.Count == 0)
            SetStatus(
                "No servers configured yet. Add a server URL below.",
                isError: false,
                autoHide: false
            );
        else
            SetStatus("Server removed.", isError: false);
    }

    // ──────────────────────────────────────────────────────────────
    // Status message with optional auto-hide
    // ──────────────────────────────────────────────────────────────

    private void SetStatus(string? message, bool isError, bool autoHide = true)
    {
        _statusCts?.Cancel();
        _statusCts = null;

        StatusMessage = message;
        IsStatusError = isError;

        // Auto-hide success messages after 4 s
        if (!isError && autoHide && message != null)
        {
            var cts = new CancellationTokenSource();
            _statusCts = cts;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(4000, cts.Token);
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (!cts.IsCancellationRequested)
                            StatusMessage = null;
                    });
                }
                catch (OperationCanceledException) { }
            });
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────

    private static bool IsValidUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
        && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
}
