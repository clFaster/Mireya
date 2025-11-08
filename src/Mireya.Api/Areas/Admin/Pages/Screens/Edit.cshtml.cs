using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Mireya.Database;
using Mireya.Database.Models;
using System.ComponentModel.DataAnnotations;

namespace Mireya.Api.Areas.Admin.Pages.Screens;

public class EditModel(MireyaDbContext context, ILogger<EditModel> logger) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty]
    public List<Guid> SelectedCampaignIds { get; set; } = new();

    public Guid ScreenId { get; set; }
    public bool IsActive { get; set; }
    public List<CampaignSummary> AvailableCampaigns { get; set; } = new();

    public class InputModel
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(200)]
        public string? Location { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        public ApprovalStatus ApprovalStatus { get; set; }
    }

    public class CampaignSummary
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int AssetCount { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var screen = await context.Displays
            .Include(d => d.CampaignAssignments)
            .FirstOrDefaultAsync(d => d.Id == id);
            
        if (screen == null)
        {
            return NotFound();
        }

        ScreenId = id;
        IsActive = screen.IsActive;
        Input = new InputModel
        {
            Name = screen.Name,
            Location = screen.Location,
            Description = screen.Description,
            ApprovalStatus = screen.ApprovalStatus
        };

        // Load assigned campaigns
        SelectedCampaignIds = screen.CampaignAssignments.Select(ca => ca.CampaignId).ToList();

        // Load all available campaigns
        AvailableCampaigns = await context.Campaigns
            .Select(c => new CampaignSummary
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                AssetCount = c.CampaignAssets.Count
            })
            .OrderBy(c => c.Name)
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        if (!ModelState.IsValid)
        {
            ScreenId = id;
            await LoadDataAsync(id);
            return Page();
        }

        var screen = await context.Displays
            .Include(d => d.CampaignAssignments)
            .FirstOrDefaultAsync(d => d.Id == id);
            
        if (screen == null)
        {
            return NotFound();
        }

        screen.Name = Input.Name;
        screen.Location = Input.Location ?? string.Empty;
        screen.Description = Input.Description;
        screen.ApprovalStatus = Input.ApprovalStatus;
        // IsActive is set by the device, not editable here

        // Update campaign assignments
        // Remove old assignments
        context.CampaignAssignments.RemoveRange(screen.CampaignAssignments);

        // Add new assignments
        foreach (var campaignId in SelectedCampaignIds)
        {
            screen.CampaignAssignments.Add(new CampaignAssignment
            {
                CampaignId = campaignId,
                DisplayId = id,
                CreatedAt = DateTime.UtcNow
            });
        }

        try
        {
            await context.SaveChangesAsync();
            logger.LogInformation("Screen {ScreenId} updated successfully with {CampaignCount} campaign assignments", 
                id, SelectedCampaignIds.Count);
            return RedirectToPage("./Details", new { id });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating screen {ScreenId}", id);
            ModelState.AddModelError(string.Empty, "An error occurred while saving changes.");
            ScreenId = id;
            await LoadDataAsync(id);
            return Page();
        }
    }

    private async Task LoadDataAsync(Guid id)
    {
        var screen = await context.Displays
            .Include(d => d.CampaignAssignments)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (screen != null)
        {
            IsActive = screen.IsActive;
            SelectedCampaignIds = screen.CampaignAssignments.Select(ca => ca.CampaignId).ToList();
        }

        AvailableCampaigns = await context.Campaigns
            .Select(c => new CampaignSummary
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                AssetCount = c.CampaignAssets.Count
            })
            .OrderBy(c => c.Name)
            .ToListAsync();
    }
}
