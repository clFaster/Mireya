namespace Mireya.Api.Middleware;

/// <summary>
/// Middleware to debug API responses, especially for unauthorized and error responses
/// </summary>
public class ResponseDebugMiddleware(RequestDelegate next, ILogger<ResponseDebugMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        // Capture the original response body stream
        var originalBodyStream = context.Response.Body;

        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        try
        {
            // Continue down the middleware pipeline
            await next(context);

            // Log response details for debugging
            LogResponseDetails(context, responseBody);

            // Copy the response back to the original stream
            responseBody.Seek(0, SeekOrigin.Begin);
            await responseBody.CopyToAsync(originalBodyStream);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception in request pipeline for {Path}", context.Request.Path);
            throw;
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }

    private void LogResponseDetails(HttpContext context, MemoryStream responseBody)
    {
        var statusCode = context.Response.StatusCode;
        var method = context.Request.Method;
        var path = context.Request.Path;
        var queryString = context.Request.QueryString;

        // Log all responses with status codes indicating potential issues
        if (statusCode >= 400)
        {
            var logLevel = statusCode switch
            {
                401 => LogLevel.Warning,
                403 => LogLevel.Warning,
                >= 500 => LogLevel.Error,
                _ => LogLevel.Information
            };

            responseBody.Seek(0, SeekOrigin.Begin);
            var responseBodyText = new StreamReader(responseBody).ReadToEnd();
            responseBody.Seek(0, SeekOrigin.Begin);

            var authHeader = context.Request.Headers["Authorization"].ToString();
            var hasAuth = !string.IsNullOrEmpty(authHeader);
            var authType = hasAuth ? authHeader.Split(' ').FirstOrDefault() : "None";

            logger.Log(logLevel,
                "API Response Debug | Status: {StatusCode} | Method: {Method} | Path: {Path}{QueryString} | " +
                "Auth: {AuthType} | User: {User} | ContentType: {ContentType} | ResponseLength: {Length} | " +
                "Response: {Response}",
                statusCode,
                method,
                path,
                queryString,
                authType,
                context.User?.Identity?.Name ?? "Anonymous",
                context.Response.ContentType,
                responseBodyText.Length,
                responseBodyText.Length > 1000 ? responseBodyText.Substring(0, 1000) + "..." : responseBodyText
            );

            // Additional debug info for 401 Unauthorized
            if (statusCode == 401)
            {
                logger.LogWarning(
                    "Unauthorized Access Details | IsAuthenticated: {IsAuthenticated} | " +
                    "AuthScheme: {AuthScheme} | Claims: {Claims}",
                    context.User?.Identity?.IsAuthenticated ?? false,
                    context.User?.Identity?.AuthenticationType ?? "None",
                    context.User?.Claims?.Select(c => $"{c.Type}={c.Value}").Take(5)
                );
            }
        }
        else if (logger.IsEnabled(LogLevel.Debug))
        {
            // Log successful responses only at Debug level to avoid noise
            logger.LogDebug(
                "API Response | Status: {StatusCode} | Method: {Method} | Path: {Path}{QueryString} | " +
                "User: {User}",
                statusCode,
                method,
                path,
                queryString,
                context.User?.Identity?.Name ?? "Anonymous"
            );
        }
    }
}

/// <summary>
/// Extension methods for registering the ResponseDebugMiddleware
/// </summary>
public static class ResponseDebugMiddlewareExtensions
{
    public static IApplicationBuilder UseResponseDebug(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ResponseDebugMiddleware>();
    }
}
