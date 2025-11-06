using Microsoft.EntityFrameworkCore;
using Mireya.Database;
using Mireya.Database.Models;

namespace Mireya.Api.Services.Asset;

public interface IAssetService
{
    Task<List<AssetSummary>> UploadAssetsAsync(List<IFormFile> files);
    Task<AssetSummary> CreateWebsiteAssetAsync(string url, string name, string? description);
    Task<PagedAssets> GetAssetsAsync(int page, int pageSize, AssetType? type, string sortBy);
    Task DeleteAssetAsync(Guid id);
    Task<Database.Models.Asset> UpdateAssetMetadataAsync(Guid id, UpdateAssetMetadataRequest request);
}

public class AssetService(MireyaDbContext db, IWebHostEnvironment env) : IAssetService
{
    private readonly string _uploadsFolder = Path.Combine(env.ContentRootPath, "uploads");
    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp"];
    private static readonly string[] VideoExtensions = [".mp4", ".webm", ".avi", ".mov"];
    
    private const long MaxImageSizeBytes = 10 * 1024 * 1024; // 10 MB
    private const long MaxVideoSizeBytes = 100 * 1024 * 1024; // 100 MB

    public async Task<List<AssetSummary>> UploadAssetsAsync(List<IFormFile> files)
    {
        if (files == null || files.Count == 0)
            throw new ArgumentException("No files provided");

        Directory.CreateDirectory(_uploadsFolder);

        var assets = new List<Database.Models.Asset>();
        var errors = new List<string>();
        
        foreach (var file in files)
        {
            if (file.Length == 0) continue;

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var isImage = ImageExtensions.Contains(extension);
            var isVideo = VideoExtensions.Contains(extension);
            
            if (!isImage && !isVideo)
            {
                errors.Add($"{file.FileName}: Unsupported file type");
                continue;
            }

            // Validate file size
            if (isImage && file.Length > MaxImageSizeBytes)
            {
                errors.Add($"{file.FileName}: Image exceeds maximum size of 10 MB");
                continue;
            }
            
            if (isVideo && file.Length > MaxVideoSizeBytes)
            {
                errors.Add($"{file.FileName}: Video exceeds maximum size of 100 MB");
                continue;
            }

            // Validate MIME type for additional security
            var contentType = file.ContentType.ToLowerInvariant();
            if (isImage && !contentType.StartsWith("image/"))
            {
                errors.Add($"{file.FileName}: Invalid image file (MIME type mismatch)");
                continue;
            }
            
            if (isVideo && !contentType.StartsWith("video/"))
            {
                errors.Add($"{file.FileName}: Invalid video file (MIME type mismatch)");
                continue;
            }

            var fileName = Guid.NewGuid() + extension;
            var filePath = Path.Combine(_uploadsFolder, fileName);

            await using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var asset = new Database.Models.Asset
            {
                Name = Path.GetFileNameWithoutExtension(file.FileName),
                Type = isImage ? AssetType.Image : AssetType.Video,
                Source = $"/uploads/{fileName}",
                FileSizeBytes = file.Length
            };
            assets.Add(asset);
        }

        if (assets.Count == 0)
        {
            var errorMessage = errors.Any() 
                ? $"No valid files uploaded. Errors: {string.Join("; ", errors)}" 
                : "No valid image or video files provided";
            throw new ArgumentException(errorMessage);
        }

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
        if (!string.IsNullOrEmpty(filePath) && asset.Source.StartsWith("/uploads/") && File.Exists(filePath))
        {
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
    public async Task<AssetSummary> CreateWebsiteAssetAsync(string url, string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL is required", nameof(url));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required", nameof(name));

        // Validate URL format
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new ArgumentException("Invalid URL format", nameof(url));

        // Only allow HTTP and HTTPS protocols
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("Only HTTP and HTTPS URLs are allowed", nameof(url));

        // Validate length constraints
        if (name.Length > 200)
            throw new ArgumentException("Name cannot exceed 200 characters", nameof(name));

        if (description is { Length: > 1000 })
            throw new ArgumentException("Description cannot exceed 1000 characters", nameof(description));

        var asset = new Database.Models.Asset
        {
            Name = name,
            Description = description,
            Type = AssetType.Website,
            Source = url
        };

        db.Assets.Add(asset);
        await db.SaveChangesAsync();

        return new AssetSummary
        {
            Id = asset.Id,
            Name = asset.Name,
            Source = asset.Source
        };
    }
}
