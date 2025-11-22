using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Mireya.Database;
using Mireya.Database.Models;

namespace Mireya.Api.Areas.Admin.Pages.Screens;

public class DetailsModel(MireyaDbContext context) : PageModel
{
    public Display Screen { get; set; } = null!;
    public List<CampaignAssetViewModel> CampaignAssets { get; set; } = [];

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var screen = await context.Displays
            .Include(d => d.CampaignAssignments)
                .ThenInclude(ca => ca.Campaign)
                    .ThenInclude(c => c.CampaignAssets)
                        .ThenInclude(ca => ca.Asset)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (screen == null)
        {
            return NotFound();
        }

        Screen = screen;

        // Flatten all campaign assets with campaign information
        CampaignAssets = Screen.CampaignAssignments
            .SelectMany(assignment => assignment.Campaign.CampaignAssets
                .Select(ca => new CampaignAssetViewModel
                {
                    CampaignName = assignment.Campaign.Name,
                    AssetId = ca.AssetId,
                    AssetName = ca.Asset.Name,
                    AssetType = ca.Asset.Type,
                    Position = ca.Position,
                    DurationSeconds = ca.DurationSeconds ?? ca.Asset.DurationSeconds ?? 10 // Use override, then asset duration, then default 10
                }))
            .OrderBy(ca => ca.CampaignName)
            .ThenBy(ca => ca.Position)
            .ToList();

        return Page();
    }
}

public class CampaignAssetViewModel
{
    public string CampaignName { get; set; } = string.Empty;
    public Guid AssetId { get; set; }
    public string AssetName { get; set; } = string.Empty;
    public AssetType AssetType { get; set; }
    public int Position { get; set; }
    public int DurationSeconds { get; set; }
}
