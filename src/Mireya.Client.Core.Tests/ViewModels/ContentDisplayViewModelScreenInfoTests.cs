using Microsoft.Extensions.Logging.Abstractions;
using Mireya.ApiClient.Services;
using Mireya.Client.Avalonia.ViewModels;
using NSubstitute;

namespace Mireya.Client.Core.Tests.ViewModels;

public sealed class ContentDisplayViewModelScreenInfoTests
{
    [Fact]
    public void ScreenInfoIsHiddenUntilRequested()
    {
        using var viewModel = CreateViewModel();

        Assert.False(viewModel.IsScreenInfoVisible);
    }

    [Fact]
    public void PrimaryActionTogglesScreenInfoPage()
    {
        using var viewModel = CreateViewModel();

        viewModel.ToggleScreenInfo();
        Assert.True(viewModel.IsScreenInfoVisible);

        viewModel.ToggleScreenInfo();
        Assert.False(viewModel.IsScreenInfoVisible);
    }

    [Fact]
    public void CloseCommandReturnsToPlayback()
    {
        using var viewModel = CreateViewModel();
        viewModel.ShowScreenInfo();

        viewModel.CloseScreenInfoCommand.Execute(null);

        Assert.False(viewModel.IsScreenInfoVisible);
    }

    [Fact]
    public void ServerSelectionCommandRequestsNavigation()
    {
        using var viewModel = CreateViewModel();
        var navigationRequested = false;
        viewModel.ReturnToServerSelectionRequested += () => navigationRequested = true;

        viewModel.ReturnToServerSelectionCommand.Execute(null);

        Assert.True(navigationRequested);
    }

    [Fact]
    public void DisposingViewModelStopsVideoPlayback()
    {
        var viewModel = CreateViewModel();
        var videoStopRequested = false;
        viewModel.VideoStopRequested += () => videoStopRequested = true;

        viewModel.Dispose();

        Assert.True(videoStopRequested);
    }

    private static ContentDisplayViewModel CreateViewModel()
    {
        var authenticationService = Substitute.For<IAuthenticationService>();
        authenticationService
            .GetAuthenticationStateAsync()
            .Returns(Task.FromResult(AuthenticationState.Failed));

        return new ContentDisplayViewModel(
            authenticationService,
            Substitute.For<IScreenHubService>(),
            Substitute.For<ILocalAssetSyncService>(),
            NullLogger<ContentDisplayViewModel>.Instance
        );
    }
}
