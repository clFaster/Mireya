using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Mireya.Database;
using Mireya.Database.Models;

namespace Mireya.Api.Areas.Admin.Pages.Screens;

public class DetailsModel(MireyaDbContext context) : PageModel
{
    public Display? Screen { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Screen = await context.Displays.FindAsync(id);
        if (Screen == null)
        {
            return NotFound();
        }

        return Page();
    }
}
