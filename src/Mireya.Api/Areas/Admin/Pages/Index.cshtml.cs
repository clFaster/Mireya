using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Mireya.Database;
using Mireya.Database.Models;

namespace Mireya.Api.Areas.Admin.Pages;

public class IndexModel : PageModel
{
    private readonly MireyaDbContext _context;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(MireyaDbContext context, ILogger<IndexModel> logger)
    {
        _context = context;
        _logger = logger;
    }

    public int TotalScreens { get; set; }
    public int PendingScreens { get; set; }
    public int TotalAssets { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            TotalScreens = await _context.Displays.CountAsync();
            PendingScreens = await _context.Displays.CountAsync(d => d.ApprovalStatus == ApprovalStatus.Pending);
            TotalAssets = await _context.Assets.CountAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading dashboard statistics");
            TotalScreens = 0;
            PendingScreens = 0;
            TotalAssets = 0;
        }
    }
}
