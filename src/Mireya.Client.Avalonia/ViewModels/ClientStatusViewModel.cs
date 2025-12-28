using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Mireya.ApiClient.Models;
using Mireya.ApiClient.Services;
using Mireya.Client.Avalonia.Services;

namespace Mireya.Client.Avalonia.ViewModels;

public partial class ClientStatusViewModel : ViewModelBase
{
    private readonly ILocalAssetSyncService _assetSyncService;
    private readonly IAuthenticationService _authenticationService;
    private readonly IBackendManager _backendManager;
    private readonly IScreenHubService _hubService;
    private readonly ILogger<ClientStatusViewModel> _logger;

    [ObservableProperty]
    private string _authStatus = "Not checked";

    [ObservableProperty]
    private string? _currentBackendUrl;

    [ObservableProperty]
    private bool _isAuthenticated;

    [ObservableProperty]
    private bool _isStatusError;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string _syncStatus = "No sync in progress";

    public ClientStatusViewModel(
        IBackendManager backendManager,
        IAuthenticationService authenticationService,
        IScreenHubService hubService,
        ILocalAssetSyncService assetSyncService,
        ILogger<ClientStatusViewModel> logger
    )
    {
        _backendManager = backendManager;
        _authenticationService = authenticationService;
        _hubService = hubService;
        _assetSyncService = assetSyncService;
        _logger = logger;

        _logger.LogInformation("ClientStatusViewModel initialized");

        _hubService.OnConfigurationUpdateReceived += OnConfigurationUpdateReceived;
        _hubService.OnStartAssetSync += OnStartAssetSync;
        _hubService.OnReconnected += OnReconnected;

        _assetSyncService.OnSyncProgressChanged += OnSyncProgressChanged;
        _assetSyncService.OnCampaignSyncCompleted += OnCampaignSyncCompleted;
        _assetSyncService.OnAssetSyncFailed += OnAssetSyncFailed;

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        // Load current backend info
        var currentBackend = await _backendManager.GetCurrentBackendAsync();
        if (currentBackend != null)
        {
            CurrentBackendUrl = currentBackend.BaseUrl;
            StatusMessage = $"Connected to: {currentBackend.BaseUrl}";
            IsStatusError = false;
        }
        else
        {
            StatusMessage = "No backend connected";
            IsStatusError = true;
        }

        // Check authentication state
        await CheckAuthenticationAsync();
    }

    private void OnConfigurationUpdateReceived(ScreenConfiguration config)
    {
        _logger.LogInformation(
            "===== OnConfigurationUpdateReceived: {ScreenName} with {CampaignCount} campaigns =====",
            config.ScreenName,
            config.Campaigns.Count
        );

        Console.WriteLine(
            $"[MainWindowViewModel] Received configuration update for {config.ScreenName}"
        );
        Dispatcher.UIThread.Post(() =>
        {
            StatusMessage =
                $"Received Config Update: {config.ScreenName} ({config.Campaigns.Count} campaigns)";
            IsStatusError = false;
        });
    }

    private async void OnStartAssetSync(List<CampaignSyncInfo> campaigns)
    {
        _logger.LogInformation(
            "===== OnStartAssetSync: Received {CampaignCount} campaigns to sync =====",
            campaigns.Count
        );

        foreach (var campaign in campaigns)
        {
            _logger.LogInformation(
                "  Campaign: {CampaignId} - {CampaignName} with {AssetCount} assets",
                campaign.CampaignId,
                campaign.CampaignName,
                campaign.Assets.Count
            );

            foreach (var asset in campaign.Assets)
                _logger.LogDebug(
                    "    Asset: {AssetId} - {AssetName} ({Type}) - Source: {Source}",
                    asset.AssetId,
                    asset.Name,
                    asset.Type,
                    asset.Source
                );
        }

        Console.WriteLine(
            $"[MainWindowViewModel] Received StartAssetSync for {campaigns.Count} campaigns"
        );
        Dispatcher.UIThread.Post(() =>
        {
            SyncStatus = $"Starting sync for {campaigns.Count} campaigns...";
        });

        // Start syncing assets in the background
        _logger.LogInformation("Starting background sync task...");
        _ = Task.Run(async () =>
        {
            try
            {
                await _assetSyncService.SyncCampaignsAsync(campaigns);
                _logger.LogInformation("Background sync task completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background sync task failed");
            }
        });
    }

    private async void OnReconnected()
    {
        _logger.LogInformation("===== OnReconnected: SignalR reconnected =====");

        Console.WriteLine(
            "[MainWindowViewModel] SignalR reconnected - checking for missing assets"
        );
        Dispatcher.UIThread.Post(() =>
        {
            SyncStatus = "Reconnected - checking for missing assets...";
        });

        // The server will send StartAssetSync event automatically on reconnect
        // through ScreenSynchronizationService, so we don't need to manually trigger it
    }

    private void OnSyncProgressChanged(Guid assetId, string state, int progress)
    {
        _logger.LogDebug(
            "Sync progress: Asset {AssetId} - {State} ({Progress}%)",
            assetId,
            state,
            progress
        );

        Dispatcher.UIThread.Post(() =>
        {
            SyncStatus = $"Syncing asset {assetId}: {state} ({progress}%)";
        });
    }

    private void OnCampaignSyncCompleted(Guid campaignId, string campaignName)
    {
        _logger.LogInformation(
            "Campaign sync completed: {CampaignId} - {CampaignName}",
            campaignId,
            campaignName
        );

        Console.WriteLine($"[MainWindowViewModel] Campaign sync completed: {campaignName}");
        Dispatcher.UIThread.Post(() =>
        {
            SyncStatus = $"✓ Campaign '{campaignName}' synced successfully";
        });
    }

    private void OnAssetSyncFailed(Guid assetId, string errorMessage)
    {
        _logger.LogError("Asset sync failed: {AssetId} - {ErrorMessage}", assetId, errorMessage);

        Console.WriteLine($"[MainWindowViewModel] Asset sync failed: {assetId} - {errorMessage}");
        Dispatcher.UIThread.Post(() =>
        {
            SyncStatus = $"✗ Asset {assetId} sync failed: {errorMessage}";
        });
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
            _ => "Unknown",
        };

        IsAuthenticated = state == AuthenticationState.Authenticated;
    }

    [RelayCommand]
    private async Task RegisterAsync()
    {
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
    private async Task TestSyncAsync()
    {
        StatusMessage = "Testing SignalR sync...";
        IsStatusError = false;

        Console.WriteLine(
            $"[MainWindowViewModel] Testing SignalR sync - Hub connected: {_hubService.IsConnected}"
        );

        StatusMessage =
            $"SignalR Connection Status: {(_hubService.IsConnected ? "✓ Connected" : "✗ Not Connected")}";
        IsStatusError = !_hubService.IsConnected;
    }

    [RelayCommand]
    private async Task FetchScreenInfoAsync()
    {
        StatusMessage = "Fetching screen info...";
        IsStatusError = false;

        // Check if authenticated first
        var authState = await _authenticationService.GetAuthenticationStateAsync();
        if (authState != AuthenticationState.Authenticated)
        {
            StatusMessage =
                $"❌ Cannot fetch screen info: Not authenticated.\nCurrent state: {authState}\n\nPlease login first.";
            IsStatusError = true;
            return;
        }

        var screenInfo = await _authenticationService.GetScreenInfoAsync();

        if (screenInfo != null)
        {
            StatusMessage =
                $"✓ Screen Information Retrieved:\n\n"
                + $"🆔 Screen ID: {screenInfo.ScreenIdentifier}\n"
                + $"📝 Name: {screenInfo.ScreenName}\n"
                + $"📊 Status: {screenInfo.ApprovalStatus}\n"
                + $"📄 Description: {screenInfo.Description ?? "N/A"}\n\n"
                + $"💡 Tip: If status is 'Pending', ask an admin to approve this screen in the backend.";
            IsStatusError = false;
        }
        else
        {
            StatusMessage =
                "❌ Failed to fetch screen info.\n\n"
                + "Possible causes:\n"
                + "• Not authenticated (try logging in again)\n"
                + "• Token expired (try logging in again)\n"
                + "• Backend not reachable\n"
                + "• Check console output for details";
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
    private async Task ChangeBackendAsync()
    {
        // This will be handled by parent to switch views
        StatusMessage = "Switching to backend selection...";
        IsStatusError = false;
    }
}
