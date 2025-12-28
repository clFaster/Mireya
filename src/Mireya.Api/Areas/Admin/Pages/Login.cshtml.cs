using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Mireya.Database.Models;

namespace Mireya.Api.Areas.Admin.Pages;

public class LoginModel(SignInManager<User> signInManager, ILogger<LoginModel> logger) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            Response.Redirect("/Admin");
            return;
        }

        ReturnUrl = returnUrl ?? Url.Content("~/Admin");

        // Clear the existing external cookie to ensure a clean login process
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/Admin");
        ReturnUrl = returnUrl;

        if (ModelState.IsValid)
        {
            // This doesn't count login failures towards account lockout
            // To enable password failures to trigger account lockout, set lockoutOnFailure: true
            var result = await signInManager.PasswordSignInAsync(
                Input.Email,
                Input.Password,
                Input.RememberMe,
                true
            );

            if (result.Succeeded)
            {
                logger.LogInformation("User logged in");
                return LocalRedirect(returnUrl);
            }

            if (result.RequiresTwoFactor)
            {
                ErrorMessage = "Two-factor authentication is required.";
                return Page();
            }

            if (result.IsLockedOut)
            {
                logger.LogWarning("User account locked out");
                ErrorMessage = "Your account has been locked out. Please try again later.";
                return Page();
            }

            ErrorMessage = "Invalid login attempt.";
            return Page();
        }

        // If we got this far, something failed, redisplay form
        return Page();
    }

    public class InputModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember me?")]
        public bool RememberMe { get; set; }
    }
}
