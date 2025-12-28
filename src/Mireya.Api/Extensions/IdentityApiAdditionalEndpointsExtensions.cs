using Microsoft.AspNetCore.Identity;

namespace Mireya.Api.Extensions;

public static class IdentityApiAdditionalEndpointsExtensions
{
    public static IEndpointRouteBuilder MapIdentityApiAdditionalEndpoints<TUser>(
        this IEndpointRouteBuilder endpoints
    )
        where TUser : class, new()
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var routeGroup = endpoints.MapGroup("");

        var accountGroup = routeGroup.MapGroup("/account").RequireAuthorization();

        accountGroup.MapPost(
            "/logout",
            async (SignInManager<TUser> signInManager) =>
            {
                await signInManager.SignOutAsync();
                return Results.Ok();
            }
        );

        return endpoints;
    }
}
