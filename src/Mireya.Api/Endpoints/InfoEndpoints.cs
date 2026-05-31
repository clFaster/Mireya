using System.Reflection;
using Carter;

namespace Mireya.Api.Endpoints;

/// <summary>
///     Public, unauthenticated identity endpoint used by clients to verify that a given
///     base URL actually hosts a Mireya backend (instead of relying on guessing from auth challenges).
/// </summary>
public class InfoEndpoints : ICarterModule
{
    public const string ApplicationName = "Mireya";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/info", () =>
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
            return Results.Ok(new ApiInfoResponse(ApplicationName, version));
        }).AllowAnonymous();
    }
}

public record ApiInfoResponse(string Application, string Version);
