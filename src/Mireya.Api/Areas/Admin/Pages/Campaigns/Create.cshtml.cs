using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Mireya.Api.Services.Campaign;
using Mireya.Database;
using Mireya.Database.Models;

namespace Mireya.Api.Areas.Admin.Pages.Campaigns;

public class CreateModel(MireyaDbContext context, ICampaignService campaignService, ILogger<CreateModel> logger) : PageModel
{
    [BindProperty]
    public string Name { get; set; } = string.Empty;

    [BindProperty]
    public string? Description { get; set; }

    [BindProperty]
    public string SelectedAssetsJson { get; set; } = "[]";

    public List<Asset> AvailableAssets { get; set; } = [];

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        await LoadDataAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Campaign name is required.";
            await LoadDataAsync();
            return Page();
        }

        try
        {
            // Parse selected assets from JSON
            logger.LogInformation("Raw SelectedAssetsJson: {Json}", SelectedAssetsJson);
            
            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var selectedAssets = System.Text.Json.JsonSerializer.Deserialize<List<SelectedAsset>>(SelectedAssetsJson, options);
            
            if (selectedAssets == null || !selectedAssets.Any())
            {
                ErrorMessage = "Please add at least one asset to the campaign.";
                await LoadDataAsync();
                return Page();
            }

            logger.LogInformation("Creating campaign with {AssetCount} assets. Asset IDs: {AssetIds}", 
                selectedAssets.Count, 
                string.Join(", ", selectedAssets.Select(a => a.AssetId)));

            // Create campaign request
            var request = new CreateCampaignRequest(
                Name,
                Description,
                selectedAssets.Select((a, index) => new CampaignAssetDto(
                    a.AssetId,
                    index + 1, // Position is 1-based
                    a.DurationSeconds
                )).ToList(),
                new List<Guid>() // Empty display list - displays are assigned from the screen edit page
            );

            await campaignService.CreateCampaignAsync(request);

            TempData["SuccessMessage"] = "Campaign created successfully.";
            logger.LogInformation("Campaign '{CampaignName}' created successfully", Name);
            return RedirectToPage("./Index");
        }
        catch (ArgumentException ex)
        {
            ErrorMessage = ex.Message;
            logger.LogWarning(ex, "Validation error creating campaign");
            await LoadDataAsync();
            return Page();
        }
        catch (Exception ex)
        {
            ErrorMessage = "An error occurred while creating the campaign. Please try again.";
            logger.LogError(ex, "Error creating campaign");
            await LoadDataAsync();
            return Page();
        }
    }

    private async Task LoadDataAsync()
    {
        AvailableAssets = await context.Assets
            .OrderBy(a => a.Name)
            .ToListAsync();
    }
}

public class SelectedAsset
{
    public Guid AssetId { get; set; }
    public int? DurationSeconds { get; set; }
}
