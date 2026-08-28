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

public sealed class MainWindowViewModelNavigationTests
{
    [Fact]
    public async Task ReturnToServerSelectionStopsPlaybackAndDisconnects()
    {
        var backendManager = Substitute.For<IBackendManager>();
        backendManager.GetAllBackendsAsync().Returns(Task.FromResult(new List<BackendInstance>()));

        var assetSyncService = Substitute.For<ILocalAssetSyncService>();
        assetSyncService
            .GetAssetCacheInfoAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new AssetCacheInfo(0, 0)));

        var hubService = Substitute.For<IScreenHubService>();
        hubService.DisconnectAsync().Returns(Task.CompletedTask);

        var authenticationService = Substitute.For<IAuthenticationService>();
        authenticationService
            .GetAuthenticationStateAsync()
            .Returns(Task.FromResult(AuthenticationState.Failed));

        var capabilities = new ClientPlatformCapabilities { SupportsFullscreen = true };
        var appSettings = new AppSettings(Substitute.For<IServiceScopeFactory>(), capabilities);
        var services = new ServiceCollection()
            .AddSingleton(backendManager)
            .AddSingleton(Substitute.For<IApiClientConfiguration>())
            .AddSingleton<ILogger<BackendSelectionViewModel>>(
                NullLogger<BackendSelectionViewModel>.Instance
            )
            .AddSingleton(appSettings)
            .AddSingleton(capabilities)
            .AddSingleton(assetSyncService)
            .AddSingleton(hubService)
            .BuildServiceProvider();

        using var viewModel = new MainWindowViewModel(
            services,
            NullLogger<MainWindowViewModel>.Instance,
            appSettings
        );
        (viewModel.CurrentView as IDisposable)?.Dispose();

        var content = new ContentDisplayViewModel(
            authenticationService,
            hubService,
            assetSyncService,
            NullLogger<ContentDisplayViewModel>.Instance
        );
        var videoStopRequested = false;
        content.VideoStopRequested += () => videoStopRequested = true;
        viewModel.CurrentView = content;

        viewModel.ReturnToServerSelection();

        Assert.IsType<BackendSelectionViewModel>(viewModel.CurrentView);
        Assert.True(videoStopRequested);
        await hubService.Received(1).DisconnectAsync();
    }
}
