using Microsoft.Extensions.DependencyInjection;
using Mireya.ApiClient;
using Mireya.ApiClient.Services;

// Configure services
var services = new ServiceCollection();

// Register Mireya API client with local backend URL
services.AddMireyaApiClient();

var provider = services.BuildServiceProvider();

var mireyaClient = provider.GetRequiredService<IMireyaService>();

mireyaClient.SetBaseUrl("https://localhost:5001");

while (true)
{
    // Offer options
    Console.WriteLine("Select an option:");
    Console.WriteLine("0. Exit");
    Console.WriteLine("1. Startup");
    Console.WriteLine("2. Get User Info");

    switch (Console.ReadLine())
    {
        case "0":
            return;
        case "1":
            Console.Write("Startup selected.");
            break;
        case "2":
            Console.Write("Get User Info selected.");
            break;
        default:
            Console.WriteLine("Invalid option. Please try again.");
            break;
    }
}
