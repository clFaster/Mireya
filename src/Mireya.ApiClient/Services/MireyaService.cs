using Microsoft.Extensions.Options;
using Mireya.ApiClient.Options;

namespace Mireya.ApiClient.Services;

public interface IMireyaService
{
    void SetBaseUrl(string httpsLocalhost);
}

public class MireyaService(IOptions<MireyaApiClientOptions> mireyaOptions) : IMireyaService
{
    public void SetBaseUrl(string httpsLocalhost)
    {
        mireyaOptions.Value.BaseUrl = httpsLocalhost;
    }
}
