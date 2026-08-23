using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Mireya.ApiClient.Data;
using Mireya.ApiClient.Services;
using NSubstitute;

namespace Mireya.ApiClient.Tests;

public sealed class LocalAssetSyncServiceCacheTests
{
    [Fact]
    public async Task ClearAssetCache_RemovesFilesAndResetsDownloadTracking()
    {
        var cacheDirectory = Path.Combine(
            Path.GetTempPath(),
            "Mireya.ApiClient.Tests",
            Guid.NewGuid().ToString("N")
        );

        try
        {
            await using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<LocalDbContext>()
                .UseSqlite(connection)
                .Options;
            await using var db = new LocalDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var backendId = Guid.NewGuid();
            var assetId = Guid.NewGuid();
            var backendCacheDirectory = Path.Combine(cacheDirectory, backendId.ToString());
            Directory.CreateDirectory(backendCacheDirectory);
            var assetPath = Path.Combine(backendCacheDirectory, $"{assetId}.mp4");
            await File.WriteAllBytesAsync(assetPath, new byte[1536]);

            db.BackendInstances.Add(
                new BackendInstance
                {
                    Id = backendId,
                    BaseUrl = "https://mireya.example",
                    IsCurrentBackend = true,
                }
            );
            db.DownloadedAssets.Add(
                new DownloadedAsset
                {
                    BackendInstanceId = backendId,
                    AssetId = assetId,
                    LocalPath = assetPath,
                    FileExtension = ".mp4",
                    IsDownloaded = true,
                    DownloadedAt = DateTime.UtcNow,
                }
            );
            await db.SaveChangesAsync();

            var service = new LocalAssetSyncService(
                db,
                Substitute.For<IBackendManager>(),
                Substitute.For<IHttpClientFactory>(),
                Substitute.For<IAccessTokenProvider>(),
                NullLogger<LocalAssetSyncService>.Instance,
                cacheDirectory
            );

            Assert.Equal(new AssetCacheInfo(1, 1536), await service.GetAssetCacheInfoAsync());

            var removed = await service.ClearAssetCacheAsync();

            Assert.Equal(new AssetCacheInfo(1, 1536), removed);
            Assert.Empty(
                Directory.EnumerateFiles(cacheDirectory, "*", SearchOption.AllDirectories)
            );
            var trackedAsset = await db.DownloadedAssets.SingleAsync();
            Assert.False(trackedAsset.IsDownloaded);
            Assert.Null(trackedAsset.LocalPath);
            Assert.Null(trackedAsset.FileExtension);
            Assert.Null(trackedAsset.DownloadedAt);
        }
        finally
        {
            if (Directory.Exists(cacheDirectory))
                Directory.Delete(cacheDirectory, recursive: true);
        }
    }
}
