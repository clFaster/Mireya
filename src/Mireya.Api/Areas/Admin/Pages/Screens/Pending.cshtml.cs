using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Mireya.Database;
using Mireya.Database.Models;

namespace Mireya.Api.Areas.Admin.Pages.Screens;

public class PendingModel(MireyaDbContext context, ILogger<PendingModel> logger) : PageModel
{
    private const string PendingPageRoute = "./Pending";

    public List<Display> Screens { get; set; } = [];
    
    [BindProperty(SupportsGet = true)]
    public string? StatusFilter { get; set; }
    
    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;
    
    public int PageSize { get; set; } = 10;
    public int TotalScreens { get; set; }
    public int TotalPages { get; set; }
    public int PendingCount { get; set; }
    public int RejectedCount { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            var query = context.Displays.AsQueryable();

            // Apply status filter
            if (!string.IsNullOrEmpty(StatusFilter) && Enum.TryParse<ApprovalStatus>(StatusFilter, out var status))
            {
                query = query.Where(d => d.ApprovalStatus == status);
            }
            else
            {
                // Default: show both Pending and Rejected (exclude Approved)
                query = query.Where(d => d.ApprovalStatus != ApprovalStatus.Approved);
            }

            // Get counts for filter badges
            PendingCount = await context.Displays.CountAsync(d => d.ApprovalStatus == ApprovalStatus.Pending);
            RejectedCount = await context.Displays.CountAsync(d => d.ApprovalStatus == ApprovalStatus.Rejected);

            // Get total count for pagination
            TotalScreens = await query.CountAsync();
            TotalPages = (int)Math.Ceiling(TotalScreens / (double)PageSize);

            // Ensure CurrentPage is valid
            if (CurrentPage < 1) CurrentPage = 1;
            if (CurrentPage > TotalPages && TotalPages > 0) CurrentPage = TotalPages;

            // Get paginated screens
            Screens = await query
                .OrderByDescending(d => d.CreatedAt)
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading pending/rejected screens list");
            Screens = [];
        }
    }

    public async Task<IActionResult> OnPostApproveAsync(Guid id)
    {
        try
        {
            var screen = await context.Displays.FindAsync(id);
            if (screen == null)
            {
                return NotFound();
            }

            screen.ApprovalStatus = ApprovalStatus.Approved;
            screen.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();

            logger.LogInformation("Screen {ScreenId} approved via Pending page", id);
            
            return RedirectToPage(PendingPageRoute, new { StatusFilter, CurrentPage });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error approving screen {ScreenId}", id);
            return RedirectToPage(PendingPageRoute, new { StatusFilter, CurrentPage });
        }
    }

    public async Task<IActionResult> OnPostRejectAsync(Guid id)
    {
        try
        {
            var screen = await context.Displays.FindAsync(id);
            if (screen == null)
            {
                return NotFound();
            }

            screen.ApprovalStatus = ApprovalStatus.Rejected;
            screen.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();

            logger.LogInformation("Screen {ScreenId} rejected via Pending page", id);
            
            return RedirectToPage(PendingPageRoute, new { StatusFilter, CurrentPage });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error rejecting screen {ScreenId}", id);
            return RedirectToPage(PendingPageRoute, new { StatusFilter, CurrentPage });
        }
    }
}
