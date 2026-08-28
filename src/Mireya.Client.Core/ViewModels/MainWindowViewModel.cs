using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Mireya.ApiClient.Data;
using Mireya.ApiClient.Services;
using Mireya.Client.Avalonia.Platform;
using Mireya.Client.Avalonia.Services;

namespace Mireya.Client.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    public const int AutoStartDelaySeconds = 10;

    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly AppSettings _appSettings;
    private CancellationTokenSource? _autoStartCts;
    private bool _disposed;

    /// <summary>
    ///     Side-effect-free sample data for the XAML previewer. Runtime instances are
    ///     created through dependency injection using the public constructor below.
    /// </summary>
    public static MainWindowViewModel DesignInstance => new();

    [ObservableProperty]
    private ViewModelBase? _currentView;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AutoStartCountdownText))]
    private int _autoStartSecondsRemaining;

    [ObservableProperty]
    private bool _isAutoStartPending;

    public string AutoStartCountdownText =>
        $"Connecting automatically in {AutoStartSecondsRemaining} second{(AutoStartSecondsRemaining == 1 ? string.Empty : "s")}. Press any key to cancel.";

    /// <summary>
    ///     Creates sample data for XAML design tools. Runtime code should resolve this
    ///     view model through dependency injection instead.
    /// </summary>
    public MainWindowViewModel()
    {
        _serviceProvider = null!;
        _logger = NullLogger<MainWindowViewModel>.Instance;
        _appSettings = null!;
        CurrentView = ContentDisplayViewModel.DesignInstance;
    }

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
            _autoStartCts = new CancellationTokenSource();
            _ = TryAutoConnectAsync(_autoStartCts.Token)
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
        var assetSyncService = _serviceProvider.GetRequiredService<ILocalAssetSyncService>();

        CurrentView = new BackendSelectionViewModel(
            backendManager,
            apiClientConfig,
            logger,
            appSettings,
            platformCapabilities,
            assetSyncService,
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

        var content = _serviceProvider.GetRequiredService<ContentDisplayViewModel>();
        content.ReturnToServerSelectionRequested += ReturnToServerSelection;
        CurrentView = content;
    }

    /// <summary>Stops playback, disconnects from the active server, and shows server selection.</summary>
    public void ReturnToServerSelection()
    {
        if (CurrentView is not ContentDisplayViewModel)
            return;

        _logger.LogInformation("Returning to server selection");

        // Replace the playback view immediately so its timers and native renderers stop.
        ShowBackendSelection();

        _ = DisconnectFromCurrentServerAsync();
    }

    private async Task DisconnectFromCurrentServerAsync()
    {
        try
        {
            var hubService = _serviceProvider.GetRequiredService<IScreenHubService>();
            await hubService.DisconnectAsync();
            _logger.LogInformation("Disconnected from current server");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to disconnect cleanly from the current server");
        }
    }

    /// <summary>Whether the primary input can currently control the playback Screen Info page.</summary>
    public bool CanHandleScreenInfoInput => this.CurrentView is ContentDisplayViewModel;

    /// <summary>Opens Screen Info while playback is active.</summary>
    public bool TryOpenScreenInfo()
    {
        if (this.CurrentView is not ContentDisplayViewModel content || content.IsScreenInfoVisible)
            return false;

        content.ShowScreenInfo();
        return true;
    }

    /// <summary>Toggles Screen Info from a keyboard or TV remote primary action.</summary>
    public bool TryToggleScreenInfo()
    {
        if (this.CurrentView is not ContentDisplayViewModel content)
            return false;

        content.ToggleScreenInfo();
        return true;
    }

    /// <summary>Closes Screen Info for Escape, Android Back, or equivalent navigation.</summary>
    public bool TryCloseScreenInfo()
    {
        if (this.CurrentView is not ContentDisplayViewModel { IsScreenInfoVisible: true } content)
            return false;

        content.HideScreenInfo();
        return true;
    }

    // ──────────────────────────────────────────────────────────────
    // Auto-connect (used when AppSettings.AutoStart == true)
    // ──────────────────────────────────────────────────────────────

    /// <summary>Cancels the pending automatic connection after local user input.</summary>
    public void CancelAutoStart()
    {
        if (!IsAutoStartPending || _autoStartCts is not { IsCancellationRequested: false })
            return;

        _logger.LogInformation("AutoStart: cancelled by client input");
        _autoStartCts.Cancel();
        IsAutoStartPending = false;
    }

    private async Task TryAutoConnectAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "AutoStart: waiting {DelaySeconds} s before connecting...",
            AutoStartDelaySeconds
        );
        IsAutoStartPending = true;

        try
        {
            for (var remaining = AutoStartDelaySeconds; remaining > 0; remaining--)
            {
                AutoStartSecondsRemaining = remaining;
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
            IsAutoStartPending = false;

            var backendManager = _serviceProvider.GetRequiredService<IBackendManager>();
            var backends = await backendManager.GetAllBackendsAsync();
            cancellationToken.ThrowIfCancellationRequested();

            if (backends.Count == 0)
            {
                _logger.LogInformation("AutoStart: no backends configured — skipping");
                return;
            }

            // Prefer the previously-used server; fall back to the first one
            var target = backends.FirstOrDefault(b => b.IsCurrentBackend) ?? backends[0];
            _logger.LogInformation("AutoStart: probing {Url}", target.BaseUrl);

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            using var response = await http.GetAsync(
                $"{target.BaseUrl.TrimEnd('/')}/api/info",
                cancellationToken
            );
            var isOnline = false;
            if (response.IsSuccessStatusCode)
            {
                await using var stream = await response.Content.ReadAsStreamAsync(
                    cancellationToken
                );
                using var info = await JsonDocument.ParseAsync(
                    stream,
                    cancellationToken: cancellationToken
                );
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
            cancellationToken.ThrowIfCancellationRequested();
            await apiConfig.UpdateBaseUrlAsync(target.BaseUrl);
            cancellationToken.ThrowIfCancellationRequested();

            // Switch to content display on the UI thread
            Dispatcher.UIThread.Post(() =>
            {
                if (!cancellationToken.IsCancellationRequested)
                    ShowContentDisplay();
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("AutoStart: pending connection cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AutoStart: connection attempt failed");
        }
        finally
        {
            IsAutoStartPending = false;
        }
    }

    // ──────────────────────────────────────────────────────────────

    private void DisposeCurrentView()
    {
        if (CurrentView is ContentDisplayViewModel content)
            content.ReturnToServerSelectionRequested -= ReturnToServerSelection;

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
        _autoStartCts?.Cancel();
        _autoStartCts?.Dispose();
        _autoStartCts = null;
        DisposeCurrentView();
        CurrentView = null;

        GC.SuppressFinalize(this);
    }
}
