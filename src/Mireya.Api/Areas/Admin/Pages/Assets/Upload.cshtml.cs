using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Mireya.Api.Services.Asset;

namespace Mireya.Api.Areas.Admin.Pages.Assets;

public class UploadModel : PageModel
{
    private readonly IAssetService _assetService;
    private readonly ILogger<UploadModel> _logger;

    public UploadModel(IAssetService assetService, ILogger<UploadModel> logger)
    {
        _assetService = assetService;
        _logger = logger;
    }

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
            var uploadedAssets = await _assetService.UploadAssetsAsync(Files);
            SuccessMessage = $"Successfully uploaded {uploadedAssets.Count} asset(s).";
            
            // Clear the form
            Files = new();
            
            return Page();
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid file upload attempt");
            ErrorMessage = ex.Message;
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading assets");
            ErrorMessage = "An error occurred while uploading files. Please try again.";
            return Page();
        }
    }
}
