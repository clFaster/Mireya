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
        public string ContentRootPath { get; set; } =
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        public string EnvironmentName { get; set; } = "Development";
    }

    private static AssetService CreateService(TestDatabase db) =>
        new(db.Context, new FakeHostEnvironment(), Substitute.For<IAuditService>());

    private static void Seed(TestDatabase db)
    {
        db.Context.Assets.AddRange(
            new Asset
            {
                Name = "Lobby Banner",
                Type = AssetType.Image,
                Source = "/a.png",
                Tags = "lobby, promo",
            },
            new Asset
            {
                Name = "Cafeteria Menu",
                Type = AssetType.Image,
                Source = "/b.png",
                Tags = "food",
            },
            new Asset
            {
                Name = "Promo Video",
                Type = AssetType.Video,
                Source = "/c.mp4",
            }
        );
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
            asset.Id,
            new UpdateAssetMetadataRequest { Tags = " sale ,, sale, Outdoor " }
        );

        Assert.Equal("sale, Outdoor", updated.Tags);
    }

    [Theory]
    [InlineData("90", 90)]
    [InlineData("180", 180)]
    [InlineData("270", 270)]
    [InlineData("-90", 270)]
    [InlineData("360", 0)]
    public void ParseRotationDegrees_ReadsLegacyRotateTag(string rotation, int expected)
    {
        var json = $$"""
            { "streams": [{ "tags": { "rotate": "{{rotation}}" } }] }
            """;

        Assert.Equal(expected, VideoOrientationNormalizer.ParseRotationDegrees(json));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(270)]
    public void ParseRotationDegrees_ReadsDisplayMatrixSideData(int rotation)
    {
        var json = $$"""
            { "streams": [{ "side_data_list": [{ "side_data_type": "Display Matrix", "rotation": {{rotation}} }] }] }
            """;

        Assert.Equal(rotation, VideoOrientationNormalizer.ParseRotationDegrees(json));
    }

    [Fact]
    public void ParseRotationDegrees_PrefersDisplayMatrixOverLegacyTag()
    {
        const string json = """
            {
              "streams": [{
                "tags": { "rotate": "90" },
                "side_data_list": [{ "side_data_type": "Display Matrix", "rotation": 180 }]
              }]
            }
            """;

        Assert.Equal(180, VideoOrientationNormalizer.ParseRotationDegrees(json));
    }
}
