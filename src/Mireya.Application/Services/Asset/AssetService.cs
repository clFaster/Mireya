using Microsoft.EntityFrameworkCore;
using Mireya.Database;
using Mireya.Database.Models;
using Xabe.FFmpeg;

namespace Mireya.Application.Services.Asset;

public record AssetFilter(int Page = 1, int PageSize = 10, AssetType? Type = null, string SortBy = "name", string? Search = null);

public interface IAssetService
{
    Task<List<AssetSummary>> UploadAssetsAsync(List<IFormFile> files);
    Task<AssetSummary> CreateWebsiteAssetAsync(string url, string name, string? description);
    Task<PagedAssets> GetAssetsAsync(AssetFilter filter);
    Task DeleteAssetAsync(Guid id);
    Task<Database.Models.Asset> UpdateAssetMetadataAsync(
        Guid id,
        UpdateAssetMetadataRequest request
    );
}

public class AssetService(MireyaDbContext db, IHostEnvironment env) : IAssetService
{
    private const long MaxImageSizeBytes = 10 * 1024 * 1024; // 10 MB
    private const long MaxVideoSizeBytes = 100 * 1024 * 1024; // 100 MB
    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp"];
    private static readonly string[] VideoExtensions = [".mp4", ".webm", ".avi", ".mov"];
    private readonly string _uploadsFolder = Path.Combine(env.ContentRootPath, "uploads");

    public async Task<List<AssetSummary>> UploadAssetsAsync(List<IFormFile> files)
    {
        if (files == null || files.Count == 0)
            throw new ArgumentException("No files provided");

        Directory.CreateDirectory(_uploadsFolder);

        var assets = new List<Database.Models.Asset>();
        var errors = new List<string>();

        foreach (var file in files)
            await TryProcessUploadedFileAsync(file, assets, errors);

        if (assets.Count == 0)
        {
            var errorMessage = errors.Count != 0
                ? $"No valid files uploaded. Errors: {string.Join("; ", errors)}"
                : "No valid image or video files provided";
            throw new ArgumentException(errorMessage);
        }

        db.Assets.AddRange(assets);
        await db.SaveChangesAsync();

        return assets
            .Select(a => new AssetSummary { Id = a.Id, Name = a.Name, Source = a.Source })
            .ToList();
    }

    private async Task TryProcessUploadedFileAsync(IFormFile file, List<Database.Models.Asset> assets, List<string> errors)
    {
        if (file.Length == 0)
            return;

        var validationError = ValidateFile(file);
        if (validationError != null)
        {
            errors.Add(validationError);
            return;
        }

        var (asset, error) = await ProcessFileAsync(file);
        if (asset != null)
            assets.Add(asset);
        else if (error != null)
            errors.Add(error);
    }

    private static string? ValidateFile(IFormFile file)
    {
        var ctx = new FileValidationContext(file);
        return ValidateFileType(ctx) ?? ValidateFileSize(ctx) ?? ValidateContentType(ctx);
    }

    private static string? ValidateFileType(FileValidationContext ctx) =>
        (!ctx.IsImage && !ctx.IsVideo) ? $"{ctx.File.FileName}: Unsupported file type" : null;

    private static string? ValidateFileSize(FileValidationContext ctx)
    {
        if (ctx.IsImage && ctx.File.Length > MaxImageSizeBytes)
            return $"{ctx.File.FileName}: Image exceeds maximum size of 10 MB";
        if (ctx.IsVideo && ctx.File.Length > MaxVideoSizeBytes)
            return $"{ctx.File.FileName}: Video exceeds maximum size of 100 MB";
        return null;
    }

    private static string? ValidateContentType(FileValidationContext ctx)
    {
        var contentType = ctx.File.ContentType.ToLowerInvariant();
        if (ctx.IsImage && !contentType.StartsWith("image/"))
            return $"{ctx.File.FileName}: Invalid image file (MIME type mismatch)";
        if (ctx.IsVideo && !contentType.StartsWith("video/"))
            return $"{ctx.File.FileName}: Invalid video file (MIME type mismatch)";
        return null;
    }

    private async Task<(Database.Models.Asset? asset, string? error)> ProcessFileAsync(IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var isImage = ImageExtensions.Contains(extension);
        var fileName = Guid.NewGuid() + extension;
        var filePath = Path.Combine(_uploadsFolder, fileName);

        await using (var stream = new FileStream(filePath, FileMode.Create))
            await file.CopyToAsync(stream);

        int? videoDurationSeconds = null;
        string? durationError = null;
        string? thumbnailSource = isImage ? $"/uploads/{fileName}" : null;

        if (!isImage)
        {
            (videoDurationSeconds, durationError) = await ExtractVideoDurationAsync(filePath, file.FileName);
            thumbnailSource = await GenerateVideoThumbnailAsync(filePath, fileName, file.FileName);
        }

        var asset = new Database.Models.Asset
        {
            Name = Path.GetFileNameWithoutExtension(file.FileName),
            Type = isImage ? AssetType.Image : AssetType.Video,
            Source = $"/uploads/{fileName}",
            ThumbnailSource = thumbnailSource,
            FileSizeBytes = file.Length,
            DurationSeconds = videoDurationSeconds,
        };

        return (asset, durationError);
    }

    private async Task<string?> GenerateVideoThumbnailAsync(string filePath, string fileName, string originalName)
    {
        var thumbnailName = $"{Path.GetFileNameWithoutExtension(fileName)}_thumb.jpg";
        var thumbnailPath = Path.Combine(_uploadsFolder, thumbnailName);

        try
        {
            var conversion = await FFmpeg.Conversions.FromSnippet.Snapshot(
                filePath, thumbnailPath, TimeSpan.FromSeconds(1));
            await conversion.Start();
            return File.Exists(thumbnailPath) ? $"/uploads/{thumbnailName}" : null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[AssetService] Failed to generate thumbnail for '{originalName}': {ex.Message}");
            return null;
        }
    }

    private static async Task<(int? duration, string? error)> ExtractVideoDurationAsync(string filePath, string fileName)
    {
        try
        {
            var mediaInfo = await FFmpeg.GetMediaInfo(filePath);
            return ((int)Math.Round(mediaInfo.Duration.TotalSeconds), null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[AssetService] Failed to extract duration for '{fileName}': {ex.Message}");
            return (null, $"{fileName}: Could not extract duration (will use default)");
        }
    }

    private sealed record FileValidationContext(IFormFile File)
    {
        private string Extension => Path.GetExtension(File.FileName).ToLowerInvariant();
        public bool IsImage => ImageExtensions.Contains(Extension);
        public bool IsVideo => VideoExtensions.Contains(Extension);
    }

    private static IQueryable<Database.Models.Asset> ApplyAssetSorting(
        IQueryable<Database.Models.Asset> query, AssetFilter filter) =>
        string.Equals(filter.SortBy, "date", StringComparison.OrdinalIgnoreCase)
            ? query.OrderByDescending(a => a.CreatedAt)
            : query.OrderBy(a => a.Name);

    public async Task<PagedAssets> GetAssetsAsync(AssetFilter filter)
    {
        var page = filter.Page < 1 ? 1 : filter.Page;
        var pageSize = filter.PageSize < 1 ? 10 : Math.Min(filter.PageSize, 10_000);

        var query = db.Assets.AsQueryable();

        if (filter.Type.HasValue)
            query = query.Where(a => a.Type == filter.Type.Value);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(a =>
                EF.Functions.Like(a.Name, $"%{term}%")
                || (a.Description != null && EF.Functions.Like(a.Description, $"%{term}%"))
                || (a.Tags != null && EF.Functions.Like(a.Tags, $"%{term}%")));
        }

        query = ApplyAssetSorting(query, filter);

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return new PagedAssets
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Items = items,
        };
    }

    public async Task DeleteAssetAsync(Guid id)
    {
        var asset = await db.Assets.FindAsync(id);
        if (asset == null)
            throw new KeyNotFoundException("Asset not found");

        // Check if asset is used in any campaigns
        var campaignsUsingAsset = await db
            .CampaignAssets.Where(ca => ca.AssetId == id)
            .Include(ca => ca.Campaign)
            .Select(ca => ca.Campaign.Name)
            .Distinct()
            .ToListAsync();

        if (campaignsUsingAsset.Any())
        {
            var campaignList = string.Join(", ", campaignsUsingAsset);
            throw new InvalidOperationException(
                $"Cannot delete asset. It is used in the following campaigns: {campaignList}"
            );
        }

        var filePath = Path.Combine(_uploadsFolder, asset.Source["/uploads/".Length..]);
        if (IsUploadedFile(asset.Source, filePath))
            File.Delete(filePath);

        if (!string.IsNullOrEmpty(asset.ThumbnailSource) && asset.ThumbnailSource != asset.Source)
        {
            var thumbnailPath = Path.Combine(_uploadsFolder, asset.ThumbnailSource["/uploads/".Length..]);
            if (IsUploadedFile(asset.ThumbnailSource, thumbnailPath))
                File.Delete(thumbnailPath);
        }

        db.Assets.Remove(asset);
        await db.SaveChangesAsync();
    }

    public async Task<Database.Models.Asset> UpdateAssetMetadataAsync(
        Guid id,
        UpdateAssetMetadataRequest request
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        var asset = await db.Assets.FindAsync(id);
        if (asset == null)
            throw new KeyNotFoundException("Asset not found");

        if (!string.IsNullOrWhiteSpace(request.Name))
            asset.Name = request.Name;

        if (request.Description != null)
            asset.Description = request.Description;

        if (request.DurationSeconds.HasValue)
            asset.DurationSeconds =
                request.DurationSeconds.Value > 0 ? request.DurationSeconds.Value : null;

        if (request.IsMuted.HasValue)
            asset.IsMuted = request.IsMuted.Value;

        if (request.Tags != null)
            asset.Tags = NormalizeTags(request.Tags);

        asset.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return asset;
    }

    private static bool IsUploadedFile(string source, string filePath) =>
        !string.IsNullOrEmpty(filePath) && source.StartsWith("/uploads/") && File.Exists(filePath);

    private static string? NormalizeTags(string tags)
    {
        var cleaned = tags
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return cleaned.Count == 0 ? null : string.Join(", ", cleaned);
    }

    public async Task<AssetSummary> CreateWebsiteAssetAsync(
        string url,
        string name,
        string? description
    )
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
            throw new ArgumentException(
                "Description cannot exceed 1000 characters",
                nameof(description)
            );

        var asset = new Database.Models.Asset
        {
            Name = name,
            Description = description,
            Type = AssetType.Website,
            Source = url,
        };

        db.Assets.Add(asset);
        await db.SaveChangesAsync();

        return new AssetSummary
        {
            Id = asset.Id,
            Name = asset.Name,
            Source = asset.Source,
        };
    }
}
