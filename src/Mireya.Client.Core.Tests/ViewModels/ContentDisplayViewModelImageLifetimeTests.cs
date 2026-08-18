using System.Reflection;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Mireya.ApiClient.Generated;
using Mireya.ApiClient.Models;
using Mireya.ApiClient.Services;
using Mireya.Client.Avalonia.Services;
using Mireya.Client.Avalonia.ViewModels;
using NSubstitute;

namespace Mireya.Client.Core.Tests.ViewModels;

/// <summary>
///     Regression tests for the image memory leak that let the Android TV client grow until the
///     low-memory killer terminated it: an Avalonia <see cref="Bitmap" /> owns native pixel
///     memory, so every transition away from image content has to dispose the bitmap it
///     releases instead of only dropping the managed reference.
/// </summary>
public sealed class ContentDisplayViewModelImageLifetimeTests
    : IClassFixture<HeadlessSessionFixture>
{
    // Smallest possible valid PNG (1x1, fully transparent).
    private const string OnePixelPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFAAH/q842iQAAAABJRU5ErkJggg==";

    private readonly HeadlessSessionFixture _session;

    public ContentDisplayViewModelImageLifetimeTests(HeadlessSessionFixture session)
    {
        _session = session;
    }

    [Fact]
    public Task DisposedBitmapIsRecognisedAsDisposed() =>
        _session.RunAsync(() =>
        {
            // Pins the disposal probe used by the other tests: a live bitmap has to be
            // distinguishable from a disposed one.
            var bitmap = CreateBitmap();

            Assert.False(IsDisposed(bitmap));

            bitmap.Dispose();

            Assert.True(IsDisposed(bitmap));
        });

    [Fact]
    public Task ShowVideoDisposesTheImageItReplaces() =>
        _session.RunAsync(() =>
        {
            var viewModel = CreateViewModel();
            var image = CreateBitmap();
            viewModel.CurrentImage = image;

            Invoke(viewModel, "ShowVideo", new PlaylistItem { AssetType = AssetType.Video });

            try
            {
                Assert.Null(viewModel.CurrentImage);
                Assert.True(
                    IsDisposed(image),
                    "The image left behind by an image to video transition was not disposed."
                );
            }
            finally
            {
                viewModel.Dispose();
            }
        });

    [Fact]
    public Task ShowWebsiteDisposesTheImageItReplaces() =>
        _session.RunAsync(() =>
        {
            var viewModel = CreateViewModel();
            var image = CreateBitmap();
            viewModel.CurrentImage = image;

            Invoke(
                viewModel,
                "ShowWebsite",
                new PlaylistItem
                {
                    AssetType = AssetType.Website,
                    Source = "https://example.com/",
                    DurationSeconds = 10,
                }
            );

            try
            {
                Assert.Null(viewModel.CurrentImage);
                Assert.True(
                    IsDisposed(image),
                    "The image left behind by an image to website transition was not disposed."
                );
            }
            finally
            {
                viewModel.Dispose();
            }
        });

    [Fact]
    public Task ShowImageDisposesThePreviousImageAndKeepsTheNewOne() =>
        _session.RunAsync(() =>
        {
            var viewModel = CreateViewModel();
            var previous = CreateBitmap();
            viewModel.CurrentImage = previous;
            var path = WriteTempImage();

            try
            {
                Invoke(
                    viewModel,
                    "ShowImage",
                    new PlaylistItem
                    {
                        AssetType = AssetType.Image,
                        LocalPath = path,
                        DurationSeconds = 10,
                    }
                );

                Assert.NotNull(viewModel.CurrentImage);
                Assert.NotSame(previous, viewModel.CurrentImage);
                Assert.True(IsDisposed(previous), "The replaced image was not disposed.");
                Assert.False(IsDisposed(viewModel.CurrentImage!));
            }
            finally
            {
                viewModel.Dispose();
                File.Delete(path);
            }
        });

    [Fact]
    public Task ShowImageDisposesTheCurrentImageWhenTheFileIsMissing() =>
        _session.RunAsync(() =>
        {
            var viewModel = CreateViewModel();
            var image = CreateBitmap();
            viewModel.CurrentImage = image;

            Invoke(
                viewModel,
                "ShowImage",
                new PlaylistItem
                {
                    AssetType = AssetType.Image,
                    LocalPath = Path.Combine(
                        Path.GetTempPath(),
                        $"mireya-missing-{Guid.NewGuid():N}.png"
                    ),
                    DurationSeconds = 10,
                }
            );

            try
            {
                Assert.Null(viewModel.CurrentImage);
                Assert.True(
                    IsDisposed(image),
                    "The image was not disposed after a failed image load."
                );
            }
            finally
            {
                viewModel.Dispose();
            }
        });

    [Fact]
    public Task CleanupDisposesTheCurrentImage() =>
        _session.RunAsync(() =>
        {
            var viewModel = CreateViewModel();
            var image = CreateBitmap();
            viewModel.CurrentImage = image;

            viewModel.Cleanup();

            Assert.Null(viewModel.CurrentImage);
            Assert.True(IsDisposed(image), "The image was not disposed during cleanup.");
        });

    [Fact]
    public Task ReassigningTheSameImageDoesNotDisposeIt() =>
        _session.RunAsync(() =>
        {
            var viewModel = CreateViewModel();
            var image = CreateBitmap();
            viewModel.CurrentImage = image;

            try
            {
                InvokeSetCurrentImage(viewModel, image);

                Assert.Same(image, viewModel.CurrentImage);
                Assert.False(
                    IsDisposed(image),
                    "The image in use was disposed while it was still displayed."
                );
            }
            finally
            {
                viewModel.Dispose();
            }
        });

    // ──────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────

    private static ContentDisplayViewModel CreateViewModel()
    {
        var authenticationService = Substitute.For<IAuthenticationService>();
        // Short-circuits the background initialization started by the constructor.
        authenticationService
            .GetAuthenticationStateAsync()
            .Returns(Task.FromResult(AuthenticationState.Failed));

        return new ContentDisplayViewModel(
            authenticationService,
            Substitute.For<IScreenHubService>(),
            Substitute.For<ILocalAssetSyncService>(),
            NullLogger<ContentDisplayViewModel>.Instance,
            new AppSettings(Substitute.For<IServiceScopeFactory>())
        );
    }

    private static Bitmap CreateBitmap()
    {
        using var stream = new MemoryStream(Convert.FromBase64String(OnePixelPngBase64));
        return new Bitmap(stream);
    }

    private static string WriteTempImage()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mireya-image-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(path, Convert.FromBase64String(OnePixelPngBase64));
        return path;
    }

    /// <summary>
    ///     A disposed Avalonia bitmap has released its platform reference, so accessing the
    ///     underlying image reports the object as disposed.
    /// </summary>
    private static bool IsDisposed(Bitmap bitmap)
    {
        try
        {
            _ = bitmap.Size;
            return false;
        }
        catch (ObjectDisposedException)
        {
            return true;
        }
    }

    private static void Invoke(ContentDisplayViewModel viewModel, string method, PlaylistItem item)
    {
        var target =
            typeof(ContentDisplayViewModel).GetMethod(
                method,
                BindingFlags.Instance | BindingFlags.NonPublic
            ) ?? throw new InvalidOperationException($"{method} was not found on the view model.");

        target.Invoke(viewModel, [item]);
    }

    private static void InvokeSetCurrentImage(ContentDisplayViewModel viewModel, Bitmap? image)
    {
        var target =
            typeof(ContentDisplayViewModel).GetMethod(
                "SetCurrentImage",
                BindingFlags.Instance | BindingFlags.NonPublic
            )
            ?? throw new InvalidOperationException(
                "SetCurrentImage was not found on the view model."
            );

        target.Invoke(viewModel, [image]);
    }
}
