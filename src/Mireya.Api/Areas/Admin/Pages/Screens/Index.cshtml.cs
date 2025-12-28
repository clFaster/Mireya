using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Mireya.Api.Services;
using Mireya.Database;
using Mireya.Database.Models;

namespace Mireya.Api.Areas.Admin.Pages.Screens;

public class ScreensIndexModel(
    MireyaDbContext context,
    ILogger<ScreensIndexModel> logger,
    IScreenConnectionTracker connectionTracker
) : PageModel
{
    public List<Display> Screens { get; set; } = [];

    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public bool ShowOffline { get; set; } = false;

    public int PageSize { get; set; } = 10;
    public int TotalScreens { get; set; }
    public int TotalPages { get; set; }
    public int PendingCount { get; set; }
    public int OnlineCount { get; set; }
    public int OfflineCount { get; set; }
    public HashSet<string> OnlineUserIds { get; set; } = [];

    public async Task OnGetAsync()
    {
        try
        {
            // Main overview shows only Approved screens
            var query = context.Displays.Where(d => d.ApprovalStatus == ApprovalStatus.Approved);

            // Get count of pending screens for badge
            PendingCount = await context.Displays.CountAsync(d =>
                d.ApprovalStatus == ApprovalStatus.Pending
            );

            // Get all approved screens
            var allScreens = await query.ToListAsync();

            // Get connected user IDs for display purposes
            var connectedUserIds = connectionTracker.GetConnectedUserIds().ToHashSet();
            OnlineUserIds = connectedUserIds;

            // Filter by IsActive status if needed
            var filteredScreens = allScreens;
            if (!ShowOffline)
                filteredScreens = allScreens.Where(s => s.IsActive).ToList();

            // Calculate counts based on IsActive property
            OnlineCount = allScreens.Count(s => s.IsActive);
            OfflineCount = allScreens.Count - OnlineCount;

            // Get total count for pagination
            TotalScreens = filteredScreens.Count;
            TotalPages = (int)Math.Ceiling(TotalScreens / (double)PageSize);

            // Ensure CurrentPage is valid
            if (CurrentPage < 1)
                CurrentPage = 1;
            if (CurrentPage > TotalPages && TotalPages > 0)
                CurrentPage = TotalPages;

            // Get paginated screens
            Screens = filteredScreens
                .OrderByDescending(d => d.CreatedAt)
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading screens list");
            Screens = [];
        }
    }
}
