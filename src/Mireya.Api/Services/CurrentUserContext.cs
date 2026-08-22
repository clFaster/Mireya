using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Mireya.Application.Services.Audit;

namespace Mireya.Api.Services;

/// <summary>
///     Resolves the current actor for audit logging across both hosting models:
///     API requests carry an <see cref="HttpContext" />, while Blazor interactive circuits
///     expose the user via <see cref="AuthenticationStateProvider" />.
/// </summary>
public class CurrentUserContext(
    IHttpContextAccessor httpContextAccessor,
    AuthenticationStateProvider authStateProvider
) : ICurrentUserContext
{
    public async Task<(string? UserId, string? UserName)> GetCurrentUserAsync()
    {
        // Plain API requests: the authenticated principal is on the HttpContext.
        var httpUser = httpContextAccessor.HttpContext?.User;
        if (httpUser?.Identity?.IsAuthenticated == true)
            return Extract(httpUser);

        // Blazor Server interactive circuits have no HttpContext for later events;
        // fall back to the circuit's authentication state.
        try
        {
            var state = await authStateProvider.GetAuthenticationStateAsync();
            if (state.User.Identity?.IsAuthenticated == true)
                return Extract(state.User);
        }
        catch
        {
            // No usable auth state (e.g. resolved outside a circuit) — treat as unknown.
        }

        return (null, null);
    }

    private static (string?, string?) Extract(ClaimsPrincipal user) =>
        (
            user.FindFirstValue(ClaimTypes.NameIdentifier),
            user.Identity?.Name ?? user.FindFirstValue(ClaimTypes.Email)
        );
}
