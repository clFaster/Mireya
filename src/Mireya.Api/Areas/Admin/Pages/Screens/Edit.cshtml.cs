using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Mireya.Api.Services;
using Mireya.Database;
using Mireya.Database.Models;

namespace Mireya.Api.Areas.Admin.Pages.Screens;

public class EditModel(
    MireyaDbContext context,
    ILogger<EditModel> logger,
    IScreenSynchronizationService syncService
) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty]
    public List<Guid> SelectedCampaignIds { get; set; } = [];

    public Guid ScreenId { get; set; }
    public bool IsActive { get; set; }
    public List<CampaignSummary> AvailableCampaigns { get; set; } = [];

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var screen = await context
            .Displays.Include(d => d.CampaignAssignments)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (screen == null)
            return NotFound();

        ScreenId = id;
        IsActive = screen.IsActive;
        Input = new InputModel
        {
            Name = screen.Name,
            Location = screen.Location,
            Description = screen.Description,
            ApprovalStatus = screen.ApprovalStatus,
        };

        // Load assigned campaigns
        SelectedCampaignIds = screen.CampaignAssignments.Select(ca => ca.CampaignId).ToList();

        // Load all available campaigns
        AvailableCampaigns = await context
            .Campaigns.Select(c => new CampaignSummary
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                AssetCount = c.CampaignAssets.Count,
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

        try
        {
            // Step 1: Update the Display entity directly (separate from assignments)
            var screen = await context.Displays.Where(d => d.Id == id).FirstOrDefaultAsync();

            if (screen == null)
                return NotFound();

            screen.Name = Input.Name;
            screen.Location = Input.Location ?? string.Empty;
            screen.Description = Input.Description;
            screen.ApprovalStatus = Input.ApprovalStatus;
            screen.UpdatedAt = DateTime.UtcNow;

            // Save the display changes first
            await context.SaveChangesAsync();

            // Step 2: Handle campaign assignments separately
            // Get current assignments
            var currentAssignmentIds = await context
                .CampaignAssignments.Where(ca => ca.DisplayId == id)
                .Select(ca => ca.CampaignId)
                .ToListAsync();

            var selectedCampaignIds = SelectedCampaignIds.ToHashSet();
            var currentCampaignIds = currentAssignmentIds.ToHashSet();

            // Step 3: Delete assignments that are no longer selected
            var campaignIdsToRemove = currentCampaignIds
                .Where(cid => !selectedCampaignIds.Contains(cid))
                .ToList();

            if (campaignIdsToRemove.Any())
                await context
                    .CampaignAssignments.Where(ca =>
                        ca.DisplayId == id && campaignIdsToRemove.Contains(ca.CampaignId)
                    )
                    .ExecuteDeleteAsync();

            // Step 4: Add new assignments
            var campaignIdsToAdd = selectedCampaignIds
                .Where(cid => !currentCampaignIds.Contains(cid))
                .ToList();

            if (campaignIdsToAdd.Any())
            {
                var newAssignments = campaignIdsToAdd
                    .Select(campaignId => new CampaignAssignment
                    {
                        CampaignId = campaignId,
                        DisplayId = id,
                        CreatedAt = DateTime.UtcNow,
                    })
                    .ToList();

                context.CampaignAssignments.AddRange(newAssignments);
                await context.SaveChangesAsync();
            }

            logger.LogInformation(
                "Screen {ScreenId} updated successfully with {CampaignCount} campaign assignments",
                id,
                SelectedCampaignIds.Count
            );

            // Trigger SignalR sync to notify the screen
            await syncService.SyncScreenAsync(id);

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
        var screen = await context
            .Displays.Include(d => d.CampaignAssignments)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (screen != null)
        {
            IsActive = screen.IsActive;
            SelectedCampaignIds = screen.CampaignAssignments.Select(ca => ca.CampaignId).ToList();
        }

        AvailableCampaigns = await context
            .Campaigns.Select(c => new CampaignSummary
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                AssetCount = c.CampaignAssets.Count,
            })
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

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
}
