using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mireya.ApiClient.Data;
using Mireya.ApiClient.Services;
using Mireya.Client.Avalonia.Platform;
using Mireya.Client.Avalonia.Services;

namespace Mireya.Client.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly AppSettings _appSettings;
    private bool _disposed;

    [ObservableProperty]
    private ViewModelBase? _currentView;

    public MainWindowViewModel(
        IServiceProvider serviceProvider,
        ILogger<MainWindowViewModel> logger,
        AppSettings appSettings
    )
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _appSettings = appSettings;

        _logger.LogInformation("MainWindowViewModel initialized");

        // Start with backend selection
        ShowBackendSelection();

        // Skip server selection and connect automatically if configured
        if (_appSettings.AutoStart)
        {
            _ = TryAutoConnectAsync()
                .ContinueWith(
                    t => _logger.LogError(t.Exception, "Auto-connect task faulted"),
                    TaskContinuationOptions.OnlyOnFaulted
                );
        }
    }

    private void ShowBackendSelection()
    {
        _logger.LogInformation("Showing backend selection view");

        // Dispose the previous view if it supports disposal
        DisposeCurrentView();

        var backendManager = _serviceProvider.GetRequiredService<IBackendManager>();
        var apiClientConfig = _serviceProvider.GetRequiredService<IApiClientConfiguration>();
        var logger = _serviceProvider.GetRequiredService<ILogger<BackendSelectionViewModel>>();
        var appSettings = _serviceProvider.GetRequiredService<AppSettings>();
        var platformCapabilities =
            _serviceProvider.GetRequiredService<ClientPlatformCapabilities>();

        CurrentView = new BackendSelectionViewModel(
            backendManager,
            apiClientConfig,
            logger,
            appSettings,
            platformCapabilities,
            OnBackendSelected
        );
    }

    private void OnBackendSelected(BackendInstance backend)
    {
        _logger.LogInformation(
            "Backend selected: {BackendId} - {Url}",
            backend.Id,
            backend.BaseUrl
        );

        ShowContentDisplay();
    }

    private void ShowContentDisplay()
    {
        _logger.LogInformation("Showing content display view");

        // Dispose the previous view if it supports disposal
        DisposeCurrentView();

        CurrentView = _serviceProvider.GetRequiredService<ContentDisplayViewModel>();
    }

    /// <summary>Whether the primary input can currently control the playback Screen Info page.</summary>
    public bool CanHandleScreenInfoInput => CurrentView is ContentDisplayViewModel;

    /// <summary>Opens Screen Info while playback is active.</summary>
    public bool TryOpenScreenInfo()
    {
        if (CurrentView is not ContentDisplayViewModel content || content.IsScreenInfoVisible)
            return false;

        content.ShowScreenInfo();
        return true;
    }

    /// <summary>Toggles Screen Info from a keyboard or TV remote primary action.</summary>
    public bool TryToggleScreenInfo()
    {
        if (CurrentView is not ContentDisplayViewModel content)
            return false;

        content.ToggleScreenInfo();
        return true;
    }

    /// <summary>Closes Screen Info for Escape, Android Back, or equivalent navigation.</summary>
    public bool TryCloseScreenInfo()
    {
        if (CurrentView is not ContentDisplayViewModel { IsScreenInfoVisible: true } content)
            return false;

        content.HideScreenInfo();
        return true;
    }

    // ──────────────────────────────────────────────────────────────
    // Auto-connect (used when AppSettings.AutoStart == true)
    // ──────────────────────────────────────────────────────────────

    private async Task TryAutoConnectAsync()
    {
        _logger.LogInformation("AutoStart: waiting 5 s before connecting...");
        await Task.Delay(TimeSpan.FromSeconds(5));

        try
        {
            var backendManager = _serviceProvider.GetRequiredService<IBackendManager>();
            var backends = await backendManager.GetAllBackendsAsync();

            if (backends.Count == 0)
            {
                _logger.LogInformation("AutoStart: no backends configured — skipping");
                return;
            }

            // Prefer the previously-used server; fall back to the first one
            var target = backends.FirstOrDefault(b => b.IsCurrentBackend) ?? backends[0];
            _logger.LogInformation("AutoStart: probing {Url}", target.BaseUrl);

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await http.GetAsync($"{target.BaseUrl.TrimEnd('/')}/api/info");
            var isOnline = false;
            if (response.IsSuccessStatusCode)
            {
                await using var stream = await response.Content.ReadAsStreamAsync();
                using var info = await JsonDocument.ParseAsync(stream);
                isOnline =
                    info.RootElement.TryGetProperty("application", out var application)
                    && string.Equals(
                        application.GetString(),
                        "Mireya",
                        StringComparison.OrdinalIgnoreCase
                    );
            }

            if (!isOnline)
            {
                _logger.LogInformation("AutoStart: {Url} is offline — skipping", target.BaseUrl);
                return;
            }

            _logger.LogInformation("AutoStart: {Url} is online — connecting", target.BaseUrl);

            var apiConfig = _serviceProvider.GetRequiredService<IApiClientConfiguration>();
            await backendManager.SetCurrentBackendAsync(target.Id);
            await apiConfig.UpdateBaseUrlAsync(target.BaseUrl);

            // Switch to content display on the UI thread
            Dispatcher.UIThread.Post(ShowContentDisplay);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AutoStart: connection attempt failed");
        }
    }

    // ──────────────────────────────────────────────────────────────

    private void DisposeCurrentView()
    {
        if (CurrentView is IDisposable disposable)
        {
            _logger.LogDebug("Disposing current view: {ViewType}", CurrentView.GetType().Name);
            disposable.Dispose();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _logger.LogInformation("Disposing MainWindowViewModel");
        DisposeCurrentView();
        CurrentView = null;

        GC.SuppressFinalize(this);
    }
}
