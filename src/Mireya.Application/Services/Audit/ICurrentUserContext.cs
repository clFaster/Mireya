namespace Mireya.Application.Services.Audit;

/// <summary>
///     Resolves the currently authenticated actor for audit logging. Implemented in the host
///     project so it can bridge both API requests (HttpContext) and Blazor circuits
///     (AuthenticationStateProvider).
/// </summary>
public interface ICurrentUserContext
{
    /// <summary>
    ///     Returns the current actor's identity user id and display name, or nulls when unknown.
    /// </summary>
    Task<(string? UserId, string? UserName)> GetCurrentUserAsync();
}
