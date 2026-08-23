using Avalonia.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Mireya.ApiClient.Models;
using Mireya.ApiClient.Services;
using Mireya.Client.Avalonia.ViewModels;
using NSubstitute;

namespace Mireya.Client.Core.Tests.ViewModels;

[Collection(HeadlessSessionCollection.Name)]
public sealed class ContentDisplayViewModelApprovalTests
{
    private readonly HeadlessSessionFixture _session;

    public ContentDisplayViewModelApprovalTests(HeadlessSessionFixture session)
    {
        _session = session;
    }

    [Theory]
    [InlineData("Pending", "Waiting for an administrator")]
    [InlineData("Rejected", "This screen was rejected")]
    public Task UnapprovedConfiguration_ImmediatelyStopsPlayback(
        string approvalStatus,
        string expectedStatus
    ) =>
        _session.RunAsync(async () =>
        {
            var authentication = Substitute.For<IAuthenticationService>();
            authentication
                .GetAuthenticationStateAsync()
                .Returns(Task.FromResult(AuthenticationState.Failed));
            var hub = Substitute.For<IScreenHubService>();
            using var viewModel = new ContentDisplayViewModel(
                authentication,
                hub,
                Substitute.For<ILocalAssetSyncService>(),
                NullLogger<ContentDisplayViewModel>.Instance
            )
            {
                CurrentContentType = ContentType.Website,
                CurrentWebsiteUrl = "https://example.com",
                CurrentWebsiteUri = new Uri("https://example.com"),
                TotalAssets = 1,
            };
            var videoStopRequested = false;
            viewModel.VideoStopRequested += () => videoStopRequested = true;

            hub.OnConfigurationUpdateReceived += Raise.Event<Action<ScreenConfiguration>>(
                new ScreenConfiguration
                {
                    ScreenId = Guid.NewGuid(),
                    ScreenName = "Unapproved screen",
                    ApprovalStatus = approvalStatus,
                    Campaigns = [],
                }
            );
            await Dispatcher.UIThread.InvokeAsync(() => { });

            Assert.True(viewModel.IsAwaitingApproval);
            Assert.Contains(expectedStatus, viewModel.StatusText);
            Assert.Equal(ContentType.None, viewModel.CurrentContentType);
            Assert.Equal(0, viewModel.TotalAssets);
            Assert.Null(viewModel.CurrentWebsiteUrl);
            Assert.Null(viewModel.CurrentWebsiteUri);
            Assert.True(videoStopRequested);
        });
}
