using Microsoft.AspNetCore.Identity;
using Mireya.Database.Models;

namespace Mireya.Api.Endpoints;

public static class LoginEndpoints
{
    public static RouteGroupBuilder MapLoginEndpoints(this RouteGroupBuilder group)
    {
        group
            .MapPost(
                "/login",
                async (HttpContext context, SignInManager<User> signInManager, string? returnUrl) =>
                {
                    var form = await context.Request.ReadFormAsync();
                    var email = form["email"].ToString();
                    var password = form["password"].ToString();
                    var rememberMe = form["rememberMe"] == "true";

                    var result = await signInManager.PasswordSignInAsync(
                        email,
                        password,
                        rememberMe,
                        true
                    );
                    if (result.Succeeded)
                    {
                        // Validate returnUrl to prevent open redirect attacks
                        var safeUrl = returnUrl is { } localUrl && IsLocalUrl(localUrl)
                            ? localUrl
                            : "/";
                        return Results.Redirect(safeUrl);
                    }

                    return Results.Redirect("/login?error=invalid");
                }
            )
            .AllowAnonymous();

        group.MapPost(
            "/logout",
            async (SignInManager<User> signInManager) =>
            {
                await signInManager.SignOutAsync();
                return Results.Redirect("/login");
            }
        );

        return group;
    }

    private static bool IsLocalUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
            return false;

        // Only allow relative paths starting with /
        // Reject protocol-relative URLs (//evil.com) and absolute URLs
        return url.StartsWith('/') && (url.Length == 1 || url[1] != '/');
    }
}
