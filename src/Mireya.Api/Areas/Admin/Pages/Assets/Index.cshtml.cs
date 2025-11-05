using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Mireya.Database;
using Mireya.Database.Models;

namespace Mireya.Api.Areas.Admin.Pages.Assets;

public class AssetsIndexModel : PageModel
{
    private readonly MireyaDbContext _context;
    private readonly ILogger<AssetsIndexModel> _logger;

    public AssetsIndexModel(MireyaDbContext context, ILogger<AssetsIndexModel> logger)
    {
        _context = context;
        _logger = logger;
    }

    public List<Asset> Assets { get; set; } = new();
    
    [BindProperty(SupportsGet = true)]
    public string? TypeFilter { get; set; }
    
    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;
    
    public int PageSize { get; set; } = 12;
    public int TotalAssets { get; set; }
    public int TotalPages { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            var query = _context.Assets.AsQueryable();

            // Apply type filter
            if (!string.IsNullOrEmpty(TypeFilter) && Enum.TryParse<AssetType>(TypeFilter, out var type))
            {
                query = query.Where(a => a.Type == type);
            }

            // Get total count for pagination
            TotalAssets = await query.CountAsync();
            TotalPages = (int)Math.Ceiling(TotalAssets / (double)PageSize);

            // Ensure CurrentPage is valid
            if (CurrentPage < 1) CurrentPage = 1;
            if (CurrentPage > TotalPages && TotalPages > 0) CurrentPage = TotalPages;

            // Get paginated assets
            Assets = await query
                .OrderByDescending(a => a.CreatedAt)
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading assets list");
            Assets = new List<Asset>();
        }
    }
}
