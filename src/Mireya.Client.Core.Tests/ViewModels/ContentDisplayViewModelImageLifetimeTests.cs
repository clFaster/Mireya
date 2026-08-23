using System.Reflection;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.Logging.Abstractions;
using Mireya.ApiClient.Generated;
using Mireya.ApiClient.Services;
using Mireya.Client.Avalonia.ViewModels;
using NSubstitute;

namespace Mireya.Client.Core.Tests.ViewModels;

/// <summary>
///     Regression tests for the image memory leak that let the Android TV client grow until the
///     low-memory killer terminated it: an Avalonia <see cref="Bitmap" /> owns native pixel
///     memory, so every transition away from image content has to dispose the bitmap it
///     releases instead of only dropping the managed reference.
/// </summary>
[Collection(HeadlessSessionCollection.Name)]
public sealed class ContentDisplayViewModelImageLifetimeTests
{
    // Smallest possible valid PNG (1x1, fully transparent).
    private const string OnePixelPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFAAH/q842iQAAAABJRU5ErkJggg==";

    // Valid 2048x2 PNG. Its small encoded size keeps the test fixture lightweight while its
    // width still crosses the production decode limit.
    private const string OversizedLandscapePngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAACAAAAAACCAYAAADMgDxcAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAAnSURBVHhe7cExAQAAAMKg9U9tDB+gAAAAAAAAAAAAAAAAAAAAgL8BQAIAAa/d9EwAAAAASUVORK5CYII=";

    private readonly HeadlessSessionFixture _session;

    public ContentDisplayViewModelImageLifetimeTests(HeadlessSessionFixture session)
    {
        _session = session;
    }

    public static TheoryData<string, PlaylistItem, string> TransitionsThatMustReleaseTheImage() =>
        new()
        {
            {
                "ShowVideo",
                new PlaylistItem { AssetType = AssetType.Video },
                "The image left behind by an image to video transition was not disposed."
            },
            {
                "ShowWebsite",
                new PlaylistItem
                {
                    AssetType = AssetType.Website,
                    Source = "https://example.com/",
                    DurationSeconds = 10,
                },
                "The image left behind by an image to website transition was not disposed."
            },
            {
                "ShowImage",
                new PlaylistItem
                {
                    AssetType = AssetType.Image,
                    LocalPath = Path.Combine(
                        Path.GetTempPath(),
                        $"mireya-missing-{Guid.NewGuid():N}.png"
                    ),
                    DurationSeconds = 10,
                },
                "The image was not disposed after a failed image load."
            },
        };

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

    [Theory]
    [MemberData(nameof(TransitionsThatMustReleaseTheImage))]
    public Task TransitionAwayFromAnImageDisposesIt(
        string method,
        PlaylistItem item,
        string because
    ) =>
        _session.RunAsync(() =>
            AssertImageReleasedAfter(viewModel => InvokePrivate(viewModel, method, item), because)
        );

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
                InvokePrivate(
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
    public Task ShowImageBoundsTheDecodedPixelSize() =>
        _session.RunAsync(() =>
        {
            var viewModel = CreateViewModel();
            var path = WriteTempImage(OversizedLandscapePngBase64);

            try
            {
                InvokePrivate(
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
                Assert.Equal(1920, viewModel.CurrentImage!.PixelSize.Width);
            }
            finally
            {
                viewModel.Dispose();
                File.Delete(path);
            }
        });

    [Fact]
    public Task ShowingTheSameAssetAgainReusesTheDecodedBitmap() =>
        _session.RunAsync(() =>
        {
            var viewModel = CreateViewModel();
            var path = WriteTempImage();
            var item = new PlaylistItem
            {
                AssetId = Guid.NewGuid(),
                AssetType = AssetType.Image,
                LocalPath = path,
                DurationSeconds = 10,
            };

            try
            {
                InvokePrivate(viewModel, "ShowImage", item);
                var firstDecode = viewModel.CurrentImage;

                InvokePrivate(viewModel, "ShowImage", item);

                Assert.NotNull(firstDecode);
                Assert.Same(firstDecode, viewModel.CurrentImage);
                Assert.False(IsDisposed(firstDecode!));
            }
            finally
            {
                viewModel.Dispose();
                File.Delete(path);
            }
        });

    [Fact]
    public Task CleanupDisposesTheCurrentImage() =>
        _session.RunAsync(() =>
            AssertImageReleasedAfter(
                viewModel => viewModel.Cleanup(),
                "The image was not disposed during cleanup."
            )
        );

    [Fact]
    public Task ReassigningTheSameImageDoesNotDisposeIt() =>
        _session.RunAsync(() =>
        {
            var viewModel = CreateViewModel();
            var image = CreateBitmap();
            viewModel.CurrentImage = image;

            try
            {
                InvokePrivate(viewModel, "SetCurrentImage", image);

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

    [Fact]
    public Task ShowingTheCurrentItemSwitchesContentImmediately() =>
        _session.RunAsync(() =>
        {
            using var viewModel = CreateViewModel();
            var item = new PlaylistItem
            {
                AssetId = Guid.NewGuid(),
                AssetName = "Website",
                AssetType = AssetType.Website,
                Source = "https://example.com/",
                DurationSeconds = 10,
            };

            GetPlaylist(viewModel).Add(item);

            InvokePrivate(viewModel, "ShowCurrentItem");

            Assert.Equal(ContentType.Website, viewModel.CurrentContentType);
            Assert.Equal(new Uri(item.Source), viewModel.CurrentWebsiteUri);
            Assert.Equal(item.AssetName, viewModel.CurrentAssetName);
        });

    [Fact]
    public Task VideoPlaybackAdvancesAtNaturalEndInsteadOfUsingADurationTimer() =>
        _session.RunAsync(() =>
        {
            using var viewModel = CreateViewModel();
            var videoPath = Path.Combine(
                Path.GetTempPath(),
                $"mireya-video-{Guid.NewGuid():N}.mp4"
            );
            File.WriteAllBytes(videoPath, []);

            try
            {
                GetPlaylist(viewModel)
                    .AddRange([
                        new PlaylistItem
                        {
                            AssetId = Guid.NewGuid(),
                            AssetName = "Video",
                            AssetType = AssetType.Video,
                            LocalPath = videoPath,
                            DurationSeconds = 1,
                        },
                        new PlaylistItem
                        {
                            AssetId = Guid.NewGuid(),
                            AssetName = "Website",
                            AssetType = AssetType.Website,
                            Source = "https://example.com/",
                            DurationSeconds = 10,
                        },
                    ]);

                InvokePrivate(viewModel, "ShowCurrentItem");

                Assert.Equal(ContentType.Video, viewModel.CurrentContentType);
                Assert.Null(GetAdvanceTimer(viewModel));

                viewModel.NotifyVideoPlaybackEnded(videoPath);

                Assert.Equal(ContentType.Website, viewModel.CurrentContentType);
                Assert.Equal("Website", viewModel.CurrentAssetName);
            }
            finally
            {
                File.Delete(videoPath);
            }
        });

    // ──────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────

    private static void AssertImageReleasedAfter(
        Action<ContentDisplayViewModel> act,
        string because
    )
    {
        var viewModel = CreateViewModel();
        var image = CreateBitmap();
        viewModel.CurrentImage = image;

        try
        {
            act(viewModel);
            Assert.Null(viewModel.CurrentImage);
            Assert.True(IsDisposed(image), because);
        }
        finally
        {
            viewModel.Dispose();
        }
    }

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
            NullLogger<ContentDisplayViewModel>.Instance
        );
    }

    private static Bitmap CreateBitmap()
    {
        using var stream = new MemoryStream(Convert.FromBase64String(OnePixelPngBase64));
        return new Bitmap(stream);
    }

    private static string WriteTempImage(string base64 = OnePixelPngBase64)
    {
        var path = Path.Combine(Path.GetTempPath(), $"mireya-image-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(path, Convert.FromBase64String(base64));
        return path;
    }

    private static List<PlaylistItem> GetPlaylist(ContentDisplayViewModel viewModel)
    {
        var field =
            typeof(ContentDisplayViewModel).GetField(
                "_playlist",
                BindingFlags.Instance | BindingFlags.NonPublic
            ) ?? throw new InvalidOperationException("The playlist field was not found.");

        return (List<PlaylistItem>)field.GetValue(viewModel)!;
    }

    private static object? GetAdvanceTimer(ContentDisplayViewModel viewModel)
    {
        var field =
            typeof(ContentDisplayViewModel).GetField(
                "_advanceTimer",
                BindingFlags.Instance | BindingFlags.NonPublic
            ) ?? throw new InvalidOperationException("The advance timer field was not found.");

        return field.GetValue(viewModel);
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

    private static void InvokePrivate(
        ContentDisplayViewModel viewModel,
        string method,
        params object?[] arguments
    )
    {
        var target =
            typeof(ContentDisplayViewModel).GetMethod(
                method,
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic
            ) ?? throw new InvalidOperationException($"{method} was not found on the view model.");

        if (target.IsStatic)
        {
            target.Invoke(null, [viewModel, .. arguments]);
            return;
        }

        target.Invoke(viewModel, arguments);
    }
}
