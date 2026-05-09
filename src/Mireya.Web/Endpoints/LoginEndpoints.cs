using Microsoft.AspNetCore.Identity;
using Mireya.Database.Models;

namespace Mireya.Web.Endpoints;

public static class LoginEndpoints
{
    public static RouteGroupBuilder MapLoginEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/login", async (
            HttpContext context,
            SignInManager<User> signInManager,
            string? returnUrl) =>
        {
            var form = await context.Request.ReadFormAsync();
            var email = form["email"].ToString();
            var password = form["password"].ToString();
            var rememberMe = form["rememberMe"] == "true";

            var result = await signInManager.PasswordSignInAsync(email, password, rememberMe, true);
            if (result.Succeeded)
                return Results.Redirect(returnUrl ?? "/");

            return Results.Redirect($"/login?error=invalid");
        }).AllowAnonymous();

        group.MapPost("/logout", async (SignInManager<User> signInManager) =>
        {
            await signInManager.SignOutAsync();
            return Results.Redirect("/login");
        });

        return group;
    }
}
