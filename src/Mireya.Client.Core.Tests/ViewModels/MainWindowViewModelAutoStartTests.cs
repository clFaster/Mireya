using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Mireya.ApiClient.Data;
using Mireya.ApiClient.Services;
using Mireya.Client.Avalonia.Platform;
using Mireya.Client.Avalonia.Services;
using Mireya.Client.Avalonia.ViewModels;
using NSubstitute;

namespace Mireya.Client.Core.Tests.ViewModels;

public sealed class MainWindowViewModelAutoStartTests
{
    [Fact]
    public void ClientInputCancelsVisibleAutoStartCountdown()
    {
        var backendManager = Substitute.For<IBackendManager>();
        backendManager.GetAllBackendsAsync().Returns(Task.FromResult(new List<BackendInstance>()));
        var assetSyncService = Substitute.For<ILocalAssetSyncService>();
        assetSyncService
            .GetAssetCacheInfoAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new AssetCacheInfo(0, 0)));

        var capabilities = new ClientPlatformCapabilities { SupportsFullscreen = true };
        var appSettings = new AppSettings(Substitute.For<IServiceScopeFactory>(), capabilities)
        {
            AutoStart = true,
        };
        var services = new ServiceCollection()
            .AddSingleton(backendManager)
            .AddSingleton(Substitute.For<IApiClientConfiguration>())
            .AddSingleton<ILogger<BackendSelectionViewModel>>(
                NullLogger<BackendSelectionViewModel>.Instance
            )
            .AddSingleton(appSettings)
            .AddSingleton(capabilities)
            .AddSingleton(assetSyncService)
            .BuildServiceProvider();

        using var viewModel = new MainWindowViewModel(
            services,
            NullLogger<MainWindowViewModel>.Instance,
            appSettings
        );

        Assert.True(viewModel.IsAutoStartPending);
        Assert.Equal(
            MainWindowViewModel.AutoStartDelaySeconds,
            viewModel.AutoStartSecondsRemaining
        );
        Assert.Contains("Press any key to cancel", viewModel.AutoStartCountdownText);

        viewModel.CancelAutoStart();

        Assert.False(viewModel.IsAutoStartPending);
    }
}
