using Mireya.Client.Avalonia.ViewModels;

namespace Mireya.Client.Core.Tests.ViewModels;

public sealed class DesignViewModelTests
{
    [Fact]
    public void ContentDisplayDesignInstanceHasSampleStateAndDisposesWithoutServices()
    {
        using var viewModel = new ContentDisplayViewModel();

        Assert.Equal("Lobby Display", viewModel.ScreenName);
        Assert.Equal(ContentType.None, viewModel.CurrentContentType);
    }

    [Fact]
    public void MainWindowDesignInstanceContainsPreviewableContent()
    {
        using var viewModel = new MainWindowViewModel();

        Assert.IsType<ContentDisplayViewModel>(viewModel.CurrentView);
    }
}
