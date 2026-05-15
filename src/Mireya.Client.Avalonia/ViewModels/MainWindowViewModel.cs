using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mireya.ApiClient.Data;
using Mireya.ApiClient.Services;

namespace Mireya.Client.Avalonia.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly IServiceProvider _serviceProvider;
    private bool _disposed;

    [ObservableProperty]
    private ViewModelBase? _currentView;

    public MainWindowViewModel(
        IServiceProvider serviceProvider,
        ILogger<MainWindowViewModel> logger
    )
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        _logger.LogInformation("MainWindowViewModel initialized");

        // Start with backend selection
        ShowBackendSelection();
    }

    private void ShowBackendSelection()
    {
        _logger.LogInformation("Showing backend selection view");

        // Dispose the previous view if it supports disposal
        DisposeCurrentView();

        var backendManager = _serviceProvider.GetRequiredService<IBackendManager>();
        var apiClientConfig = _serviceProvider.GetRequiredService<IApiClientConfiguration>();
        var logger = _serviceProvider.GetRequiredService<ILogger<BackendSelectionViewModel>>();

        CurrentView = new BackendSelectionViewModel(
            backendManager,
            apiClientConfig,
            logger,
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
        if (_disposed) return;
        _disposed = true;

        _logger.LogInformation("Disposing MainWindowViewModel");
        DisposeCurrentView();
        CurrentView = null;

        GC.SuppressFinalize(this);
    }
}
