using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Mireya.Database;
using Mireya.Database.Models;

namespace Mireya.Api.Areas.Admin.Pages.Screens;

public class DetailsModel : PageModel
{
    private readonly MireyaDbContext _context;

    public DetailsModel(MireyaDbContext context)
    {
        _context = context;
    }

    public Display? Screen { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Screen = await _context.Displays.FindAsync(id);
        if (Screen == null)
        {
            return NotFound();
        }

        return Page();
    }
}
