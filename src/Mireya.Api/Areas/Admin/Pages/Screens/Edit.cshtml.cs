using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Mireya.Database;
using Mireya.Database.Models;
using System.ComponentModel.DataAnnotations;

namespace Mireya.Api.Areas.Admin.Pages.Screens;

public class EditModel : PageModel
{
    private readonly MireyaDbContext _context;
    private readonly ILogger<EditModel> _logger;

    public EditModel(MireyaDbContext context, ILogger<EditModel> logger)
    {
        _context = context;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public Guid ScreenId { get; set; }

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

        public bool IsActive { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var screen = await _context.Displays.FindAsync(id);
        if (screen == null)
        {
            return NotFound();
        }

        ScreenId = id;
        Input = new InputModel
        {
            Name = screen.Name,
            Location = screen.Location,
            Description = screen.Description,
            ApprovalStatus = screen.ApprovalStatus,
            IsActive = screen.IsActive
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        if (!ModelState.IsValid)
        {
            ScreenId = id;
            return Page();
        }

        var screen = await _context.Displays.FindAsync(id);
        if (screen == null)
        {
            return NotFound();
        }

        screen.Name = Input.Name;
        screen.Location = Input.Location;
        screen.Description = Input.Description;
        screen.ApprovalStatus = Input.ApprovalStatus;
        screen.IsActive = Input.IsActive;

        try
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Screen {ScreenId} updated successfully", id);
            return RedirectToPage("./Details", new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating screen {ScreenId}", id);
            ModelState.AddModelError(string.Empty, "An error occurred while saving changes.");
            ScreenId = id;
            return Page();
        }
    }
}
