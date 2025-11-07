using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Mireya.Database;
using Mireya.Database.Models;

namespace Mireya.Api.Areas.Admin.Pages;

public class IndexModel(MireyaDbContext context, ILogger<IndexModel> logger) : PageModel
{
    public int TotalScreens { get; set; }
    public int PendingScreens { get; set; }
    public int TotalAssets { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            TotalScreens = await context.Displays.CountAsync();
            PendingScreens = await context.Displays.CountAsync(d => d.ApprovalStatus == ApprovalStatus.Pending);
            TotalAssets = await context.Assets.CountAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading dashboard statistics");
            TotalScreens = 0;
            PendingScreens = 0;
            TotalAssets = 0;
        }
    }
}
