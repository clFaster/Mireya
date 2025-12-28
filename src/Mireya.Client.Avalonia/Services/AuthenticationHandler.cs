using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Mireya.ApiClient.Services;

namespace Mireya.Client.Avalonia.Services;

/// <summary>
/// HTTP message handler that adds Bearer token to outgoing requests
/// </summary>
public class AuthenticationHandler(IAccessTokenProvider tokenProvider) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, 
        CancellationToken cancellationToken)
    {
        // Get the current access token from the provider
        var token = tokenProvider.GetAccessToken();
        
        // Add Bearer token if available
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        
        return await base.SendAsync(request, cancellationToken);
    }
}
