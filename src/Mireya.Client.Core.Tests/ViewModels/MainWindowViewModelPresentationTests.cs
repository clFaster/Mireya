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

public sealed class MainWindowViewModelPresentationTests
{
    [Fact]
    public void NavigationSwitchesBetweenInteractiveAndPlaybackPresentation()
    {
        var backendManager = Substitute.For<IBackendManager>();
        backendManager.GetAllBackendsAsync().Returns(Task.FromResult(new List<BackendInstance>()));
        var assetSyncService = Substitute.For<ILocalAssetSyncService>();
        assetSyncService
            .GetAssetCacheInfoAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new AssetCacheInfo(0, 0)));

        var capabilities = new ClientPlatformCapabilities();
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
            .BuildServiceProvider();
        var presentation = new RecordingPresentationController();

        using var viewModel = new MainWindowViewModel(
            services,
            NullLogger<MainWindowViewModel>.Instance,
            appSettings,
            presentation
        );

        Assert.Equal(DisplayPresentation.Interactive, presentation.Current);

        var content = new ContentDisplayViewModel();
        viewModel.CurrentView = content;
        Assert.Equal(DisplayPresentation.Playback, presentation.Current);

        content.ShowScreenInfo();
        Assert.Equal(DisplayPresentation.Interactive, presentation.Current);

        content.HideScreenInfo();
        Assert.Equal(DisplayPresentation.Playback, presentation.Current);
    }

    private sealed class RecordingPresentationController : IDisplayPresentationController
    {
        public DisplayPresentation Current { get; private set; }

        public void Apply(DisplayPresentation presentation) => Current = presentation;
    }
}
