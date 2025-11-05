using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Mireya.Database;
using Mireya.Database.Models;

namespace Mireya.Api.Areas.Admin.Pages.Screens;

public class ScreensIndexModel : PageModel
{
    private readonly MireyaDbContext _context;
    private readonly ILogger<ScreensIndexModel> _logger;

    public ScreensIndexModel(MireyaDbContext context, ILogger<ScreensIndexModel> logger)
    {
        _context = context;
        _logger = logger;
    }

    public List<Display> Screens { get; set; } = new();
    
    [BindProperty(SupportsGet = true)]
    public string? StatusFilter { get; set; }
    
    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;
    
    public int PageSize { get; set; } = 10;
    public int TotalScreens { get; set; }
    public int TotalPages { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            var query = _context.Displays.AsQueryable();

            // Apply status filter
            if (!string.IsNullOrEmpty(StatusFilter) && Enum.TryParse<ApprovalStatus>(StatusFilter, out var status))
            {
                query = query.Where(d => d.ApprovalStatus == status);
            }

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
            _logger.LogError(ex, "Error loading screens list");
            Screens = new List<Display>();
        }
    }
}
