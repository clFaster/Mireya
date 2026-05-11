using System.Net.Http.Headers;

namespace Mireya.ApiClient.Services;

/// <summary>
///     HTTP message handler that attaches the Bearer token to outgoing requests
/// </summary>
public class AuthenticationHandler(IAccessTokenProvider tokenProvider) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var token = tokenProvider.GetAccessToken();

        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
