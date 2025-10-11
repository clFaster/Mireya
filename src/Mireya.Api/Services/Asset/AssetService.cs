using Microsoft.EntityFrameworkCore;
using Mireya.Database;
using Mireya.Database.Models;

namespace Mireya.Api.Services.Asset;

public interface IAssetService
{
    Task<List<AssetSummary>> UploadAssetsAsync(List<IFormFile> files);
    Task<PagedAssets> GetAssetsAsync(int page, int pageSize, AssetType? type, string sortBy);
    Task DeleteAssetAsync(Guid id);
    Task<Database.Models.Asset> UpdateAssetMetadataAsync(Guid id, UpdateAssetMetadataRequest request);
}

public class AssetService(MireyaDbContext db, IWebHostEnvironment env) : IAssetService
{
    private readonly string _uploadsFolder = Path.Combine(env.ContentRootPath, "uploads");
    private static readonly string[] SourceArray = [".jpg", ".jpeg", ".png"];

    public async Task<List<AssetSummary>> UploadAssetsAsync(List<IFormFile> files)
    {
        if (files == null || files.Count == 0)
            throw new ArgumentException("No files provided");

        Directory.CreateDirectory(_uploadsFolder);

        var assets = new List<Database.Models.Asset>();
        foreach (var file in files)
        {
            if (file.Length == 0) continue;

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!SourceArray.Contains(extension))
                continue; // Skip non-image files for now

            var fileName = Guid.NewGuid() + extension;
            var filePath = Path.Combine(_uploadsFolder, fileName);

            await using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var asset = new Database.Models.Asset
            {
                Name = Path.GetFileNameWithoutExtension(file.FileName),
                Type = AssetType.Image,
                Source = $"/uploads/{fileName}",
                FileSizeBytes = file.Length
            };
            assets.Add(asset);
        }

        if (assets.Count == 0)
            throw new ArgumentException("No valid image files provided");

        db.Assets.AddRange(assets);
        await db.SaveChangesAsync();

        return assets.Select(a => new AssetSummary { Id = a.Id, Name = a.Name, Source = a.Source }).ToList();
    }

    public async Task<PagedAssets> GetAssetsAsync(int page, int pageSize, AssetType? type, string sortBy)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 10;

        var query = db.Assets.AsQueryable();

        if (type.HasValue)
            query = query.Where(a => a.Type == type.Value);

        query = sortBy.ToLower() switch
        {
            "date" => query.OrderByDescending(a => a.CreatedAt),
            _ => query.OrderBy(a => a.Name)
        };

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return new PagedAssets { Total = total, Page = page, PageSize = pageSize, Items = items };
    }

    public async Task DeleteAssetAsync(Guid id)
    {
        var asset = await db.Assets.FindAsync(id);
        if (asset == null)
            throw new KeyNotFoundException("Asset not found");

        // Delete the file if it exists
        var filePath = Path.Combine(_uploadsFolder, asset.Source["/uploads/".Length..]);
        if (!string.IsNullOrEmpty(filePath) && asset.Source.StartsWith("/uploads/"))
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }

        db.Assets.Remove(asset);
        await db.SaveChangesAsync();
    }

    public async Task<Database.Models.Asset> UpdateAssetMetadataAsync(Guid id, UpdateAssetMetadataRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var asset = await db.Assets.FindAsync(id);
        if (asset == null)
            throw new KeyNotFoundException("Asset not found");

        if (!string.IsNullOrWhiteSpace(request.Name))
            asset.Name = request.Name;

        if (request.Description != null)
            asset.Description = request.Description;

        asset.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return asset;
    }
}