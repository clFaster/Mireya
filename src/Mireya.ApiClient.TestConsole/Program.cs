using Microsoft.Extensions.DependencyInjection;
using Mireya.ApiClient;
using Mireya.ApiClient.Authentication;

// Configure services
var services = new ServiceCollection();

// Register Mireya API client with local backend URL
services.AddMireyaApiClient(options =>
{
    options.BaseUrl = "https://localhost:5001";
    options.AutoRefreshTokens = true;
    options.RefreshBufferSeconds = 60;
});

var provider = services.BuildServiceProvider();
// var authClient = provider.GetRequiredService<MireyaAuthenticationClient>();
