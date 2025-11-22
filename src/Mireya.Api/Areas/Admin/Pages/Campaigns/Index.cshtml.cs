using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Mireya.Database;

namespace Mireya.Api.Areas.Admin.Pages.Campaigns;

public class IndexModel(MireyaDbContext context, ILogger<IndexModel> logger) : PageModel
{
    public List<CampaignListItem> Campaigns { get; set; } = [];

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            var campaigns = await context.Campaigns
                .Include(c => c.CampaignAssets)
                .Include(c => c.CampaignAssignments)
                .OrderByDescending(c => c.UpdatedAt)
                .ToListAsync();

            Campaigns = campaigns.Select(c => new CampaignListItem
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                AssetCount = c.CampaignAssets.Count,
                DisplayCount = c.CampaignAssignments.Count,
                UpdatedAt = c.UpdatedAt
            }).ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading campaigns list");
            Campaigns = [];
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        try
        {
            var campaign = await context.Campaigns.FindAsync(id);
            if (campaign == null)
            {
                ErrorMessage = "Campaign not found.";
                return RedirectToPage();
            }

            context.Campaigns.Remove(campaign);
            await context.SaveChangesAsync();

            SuccessMessage = "Campaign deleted successfully.";
            logger.LogInformation("Campaign {CampaignId} deleted successfully", id);
        }
        catch (Exception ex)
        {
            ErrorMessage = "An error occurred while deleting the campaign. Please try again.";
            logger.LogError(ex, "Error deleting campaign {CampaignId}", id);
        }

        return RedirectToPage();
    }
}

public class CampaignListItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int AssetCount { get; set; }
    public int DisplayCount { get; set; }
    public DateTime UpdatedAt { get; set; }
}
