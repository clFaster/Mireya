using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Mireya.Api.Services.Campaign;
using Mireya.Database;
using Mireya.Database.Models;

namespace Mireya.Api.Areas.Admin.Pages.Campaigns;

public class EditModel(MireyaDbContext context, ICampaignService campaignService, ILogger<EditModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    public string Name { get; set; } = string.Empty;

    [BindProperty]
    public string? Description { get; set; }

    [BindProperty]
    public string SelectedAssetsJson { get; set; } = "[]";

    public List<Asset> AvailableAssets { get; set; } = [];
    public List<ExistingCampaignAsset> ExistingAssets { get; set; } = [];

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var campaign = await context.Campaigns
            .Include(c => c.CampaignAssets)
                .ThenInclude(ca => ca.Asset)
            .Include(c => c.CampaignAssignments)
            .FirstOrDefaultAsync(c => c.Id == Id);

        if (campaign == null)
        {
            TempData["ErrorMessage"] = "Campaign not found.";
            return RedirectToPage("./Index");
        }

        Name = campaign.Name;
        Description = campaign.Description;

        ExistingAssets = campaign.CampaignAssets
            .OrderBy(ca => ca.Position)
            .Select(ca => new ExistingCampaignAsset
            {
                AssetId = ca.AssetId,
                Name = ca.Asset.Name,
                Type = ca.Asset.Type,
                Source = ca.Asset.Source,
                DurationSeconds = ca.DurationSeconds,
                IntrinsicDuration = ca.Asset.DurationSeconds
            })
            .ToList();

        // Serialize existing assets for JavaScript
        SelectedAssetsJson = System.Text.Json.JsonSerializer.Serialize(ExistingAssets.Select(a => new
        {
            assetId = a.AssetId,
            durationSeconds = a.Type == AssetType.Video ? (int?)null : (a.DurationSeconds ?? 10)
        }));

        await LoadDataAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Campaign name is required.";
            await LoadForEditAsync();
            return Page();
        }

        try
        {
            // Parse selected assets from JSON
            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var selectedAssets = System.Text.Json.JsonSerializer.Deserialize<List<SelectedAsset>>(SelectedAssetsJson, options);
            
            if (selectedAssets == null || !selectedAssets.Any())
            {
                ErrorMessage = "Please add at least one asset to the campaign.";
                await LoadForEditAsync();
                return Page();
            }

            // Create campaign request
            var request = new UpdateCampaignRequest(
                Name,
                Description,
                selectedAssets.Select((a, index) => new CampaignAssetDto(
                    a.AssetId,
                    index + 1, // Position is 1-based
                    a.DurationSeconds
                )).ToList(),
                [] // Empty display list - displays are assigned from the screen edit page
            );

            await campaignService.UpdateCampaignAsync(Id, request);

            TempData["SuccessMessage"] = "Campaign updated successfully.";
            logger.LogInformation("Campaign {CampaignId} updated successfully", Id);
            return RedirectToPage("./Index");
        }
        catch (ArgumentException ex)
        {
            ErrorMessage = ex.Message;
            logger.LogWarning(ex, "Validation error updating campaign");
            await LoadForEditAsync();
            return Page();
        }
        catch (Exception ex)
        {
            ErrorMessage = "An error occurred while updating the campaign. Please try again.";
            logger.LogError(ex, "Error updating campaign");
            await LoadForEditAsync();
            return Page();
        }
    }

    private async Task LoadDataAsync()
    {
        AvailableAssets = await context.Assets
            .OrderBy(a => a.Name)
            .ToListAsync();
    }

    private async Task LoadForEditAsync()
    {
        await LoadDataAsync();
        
        // Reload existing assets for display
        var campaign = await context.Campaigns
            .Include(c => c.CampaignAssets)
                .ThenInclude(ca => ca.Asset)
            .FirstOrDefaultAsync(c => c.Id == Id);

        if (campaign != null)
        {
            ExistingAssets = campaign.CampaignAssets
                .OrderBy(ca => ca.Position)
                .Select(ca => new ExistingCampaignAsset
                {
                    AssetId = ca.AssetId,
                    Name = ca.Asset.Name,
                    Type = ca.Asset.Type,
                    Source = ca.Asset.Source,
                    DurationSeconds = ca.DurationSeconds,
                    IntrinsicDuration = ca.Asset.DurationSeconds
                })
                .ToList();
        }
    }
}

public class ExistingCampaignAsset
{
    public Guid AssetId { get; set; }
    public string Name { get; set; } = string.Empty;
    public AssetType Type { get; set; }
    public string Source { get; set; } = string.Empty;
    public int? DurationSeconds { get; set; }
    public int? IntrinsicDuration { get; set; }
}
