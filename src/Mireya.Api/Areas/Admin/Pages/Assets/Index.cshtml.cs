using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Mireya.Api.Services.Asset;
using Mireya.Database;
using Mireya.Database.Models;

namespace Mireya.Api.Areas.Admin.Pages.Assets;

public class AssetsIndexModel(
    MireyaDbContext context,
    IAssetService assetService,
    ILogger<AssetsIndexModel> logger
) : PageModel
{
    public List<Asset> Assets { get; set; } = [];

    [BindProperty(SupportsGet = true)]
    public string? TypeFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;

    public int PageSize { get; set; } = 12;
    public int TotalAssets { get; set; }
    public int TotalPages { get; set; }

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            var query = context.Assets.AsQueryable();

            // Apply type filter
            if (
                !string.IsNullOrEmpty(TypeFilter)
                && Enum.TryParse<AssetType>(TypeFilter, out var type)
            )
                query = query.Where(a => a.Type == type);

            // Get total count for pagination
            TotalAssets = await query.CountAsync();
            TotalPages = (int)Math.Ceiling(TotalAssets / (double)PageSize);

            // Ensure CurrentPage is valid
            if (CurrentPage < 1)
                CurrentPage = 1;
            if (CurrentPage > TotalPages && TotalPages > 0)
                CurrentPage = TotalPages;

            // Get paginated assets
            Assets = await query
                .OrderByDescending(a => a.CreatedAt)
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading assets list");
            Assets = [];
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        try
        {
            await assetService.DeleteAssetAsync(id);
            SuccessMessage = "Asset deleted successfully.";
            logger.LogInformation("Asset {AssetId} deleted successfully", id);
        }
        catch (KeyNotFoundException)
        {
            ErrorMessage = "Asset not found.";
            logger.LogWarning("Attempted to delete non-existent asset {AssetId}", id);
        }
        catch (Exception ex)
        {
            ErrorMessage = "An error occurred while deleting the asset. Please try again.";
            logger.LogError(ex, "Error deleting asset {AssetId}", id);
        }

        return RedirectToPage(new { TypeFilter, CurrentPage });
    }

    public async Task<IActionResult> OnPostEditAsync(Guid assetId, string name, string? description)
    {
        try
        {
            var request = new UpdateAssetMetadataRequest { Name = name, Description = description };

            await assetService.UpdateAssetMetadataAsync(assetId, request);
            SuccessMessage = "Asset updated successfully.";
            logger.LogInformation("Asset {AssetId} updated successfully", assetId);
        }
        catch (KeyNotFoundException)
        {
            ErrorMessage = "Asset not found.";
            logger.LogWarning("Attempted to update non-existent asset {AssetId}", assetId);
        }
        catch (Exception ex)
        {
            ErrorMessage = "An error occurred while updating the asset. Please try again.";
            logger.LogError(ex, "Error updating asset {AssetId}", assetId);
        }

        return RedirectToPage(new { TypeFilter, CurrentPage });
    }
}
