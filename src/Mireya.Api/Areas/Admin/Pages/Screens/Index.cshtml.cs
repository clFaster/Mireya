using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Mireya.Database;
using Mireya.Database.Models;

namespace Mireya.Api.Areas.Admin.Pages.Screens;

public class ScreensIndexModel(MireyaDbContext context, ILogger<ScreensIndexModel> logger) : PageModel
{
    public List<Display> Screens { get; set; } = new();
    
    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;
    
    public int PageSize { get; set; } = 10;
    public int TotalScreens { get; set; }
    public int TotalPages { get; set; }
    public int PendingCount { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            // Main overview shows only Approved screens
            var query = context.Displays
                .Where(d => d.ApprovalStatus == ApprovalStatus.Approved);

            // Get count of pending screens for badge
            PendingCount = await context.Displays
                .CountAsync(d => d.ApprovalStatus == ApprovalStatus.Pending);

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
            logger.LogError(ex, "Error loading screens list");
            Screens = new List<Display>();
        }
    }
}
