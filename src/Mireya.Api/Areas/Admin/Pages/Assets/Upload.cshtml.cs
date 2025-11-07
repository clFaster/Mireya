using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Mireya.Api.Services.Asset;

namespace Mireya.Api.Areas.Admin.Pages.Assets;

public class UploadModel(IAssetService assetService, ILogger<UploadModel> logger) : PageModel
{
    [BindProperty]
    public List<IFormFile> Files { get; set; } = new();

    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Files == null || !Files.Any())
        {
            ErrorMessage = "Please select at least one file to upload.";
            return Page();
        }

        try
        {
            var uploadedAssets = await assetService.UploadAssetsAsync(Files);
            SuccessMessage = $"Successfully uploaded {uploadedAssets.Count} asset(s).";
            
            // Clear the form
            Files = new();
            
            return Page();
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid file upload attempt");
            ErrorMessage = ex.Message;
            return Page();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error uploading assets");
            ErrorMessage = "An error occurred while uploading files. Please try again.";
            return Page();
        }
    }

    public async Task<IActionResult> OnPostWebsiteAsync(string url, string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(name))
        {
            ErrorMessage = "URL and Name are required.";
            return Page();
        }

        try
        {
            await assetService.CreateWebsiteAssetAsync(url, name, description);
            SuccessMessage = $"Successfully added website '{name}'.";
            return Page();
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid website asset creation attempt");
            ErrorMessage = ex.Message;
            return Page();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating website asset");
            ErrorMessage = "An error occurred while adding the website. Please try again.";
            return Page();
        }
    }
}
