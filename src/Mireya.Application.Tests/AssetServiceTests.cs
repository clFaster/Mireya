using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Mireya.Application.Services.Asset;
using Mireya.Application.Services.Audit;
using Mireya.Database.Models;
using NSubstitute;

namespace Mireya.Application.Tests;

public class AssetServiceTests
{
    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string ApplicationName { get; set; } = "Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        public string EnvironmentName { get; set; } = "Development";
    }

    private static AssetService CreateService(TestDatabase db) =>
        new(db.Context, new FakeHostEnvironment(), Substitute.For<IAuditService>());

    private static void Seed(TestDatabase db)
    {
        db.Context.Assets.AddRange(
            new Asset { Name = "Lobby Banner", Type = AssetType.Image, Source = "/a.png", Tags = "lobby, promo" },
            new Asset { Name = "Cafeteria Menu", Type = AssetType.Image, Source = "/b.png", Tags = "food" },
            new Asset { Name = "Promo Video", Type = AssetType.Video, Source = "/c.mp4" });
        db.Context.SaveChanges();
    }

    [Fact]
    public async Task GetAssets_WithSearchTerm_MatchesNameOrTags()
    {
        using var db = new TestDatabase();
        Seed(db);
        var service = CreateService(db);

        var result = await service.GetAssetsAsync(new AssetFilter(Search: "promo"));

        Assert.Equal(2, result.Total);
        Assert.Contains(result.Items, a => a.Name == "Lobby Banner");
        Assert.Contains(result.Items, a => a.Name == "Promo Video");
    }

    [Fact]
    public async Task GetAssets_WithEmptySearch_ReturnsAll()
    {
        using var db = new TestDatabase();
        Seed(db);
        var service = CreateService(db);

        var result = await service.GetAssetsAsync(new AssetFilter(Search: "   "));

        Assert.Equal(3, result.Total);
    }

    [Fact]
    public async Task UpdateMetadata_NormalizesTags()
    {
        using var db = new TestDatabase();
        Seed(db);
        var service = CreateService(db);
        var asset = db.Context.Assets.First(a => a.Name == "Promo Video");

        var updated = await service.UpdateAssetMetadataAsync(
            asset.Id, new UpdateAssetMetadataRequest { Tags = " sale ,, sale, Outdoor " });

        Assert.Equal("sale, Outdoor", updated.Tags);
    }
}
