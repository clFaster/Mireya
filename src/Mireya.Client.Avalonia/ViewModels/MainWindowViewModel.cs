using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mireya.Client.Avalonia.Data;
using Mireya.Client.Avalonia.Services;

namespace Mireya.Client.Avalonia.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly IServiceProvider _serviceProvider;

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

        CurrentView = _serviceProvider.GetRequiredService<ContentDisplayViewModel>();
    }
}
