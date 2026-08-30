using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Mireya.ApiClient;
using Mireya.ApiClient.Data;
using Mireya.ApiClient.Options;
using Mireya.ApiClient.Services;
using Mireya.Client.Avalonia.Services;
using Mireya.Client.Avalonia.ViewModels;
using Serilog;

namespace Mireya.Client.Avalonia.Platform;

/// <summary>
///     Builds the shared display-client service graph while letting each platform head
///     provide its platform-specific asset renderer factory.
/// </summary>
public static class DisplayClientServiceProviderFactory
{
    /// <param name="defaultBackendUrl">Fallback backend URL for unattended deployments.</param>
    /// <param name="capabilities">What the active head supports and which device class it runs on.</param>
    /// <param name="configurePlatformServices">
    ///     Optional hook for head-specific registrations, such as an
    ///     <see cref="IDisplayPresentationController" /> that can actually rotate and
    ///     un-chrome the native window. Registrations made here replace the shared
    ///     defaults, so the hook runs last.
    /// </param>
    public static IServiceProvider Build<TAssetViewFactory>(
        string defaultBackendUrl,
        ClientPlatformCapabilities capabilities,
        Action<IServiceCollection>? configurePlatformServices = null
    )
        where TAssetViewFactory : class, IAssetViewFactory
    {
        var services = new ServiceCollection();
        services.AddDisplayClientServices<TAssetViewFactory>(defaultBackendUrl, capabilities);
        configurePlatformServices?.Invoke(services);
        return services.BuildServiceProvider();
    }

    private static void AddDisplayClientServices<TAssetViewFactory>(
        this IServiceCollection services,
        string defaultBackendUrl,
        ClientPlatformCapabilities capabilities
    )
        where TAssetViewFactory : class, IAssetViewFactory
    {
        services.AddSingleton(capabilities);
        services.AddSingleton<AppSettings>();

        // Replaced by the platform head when it can control orientation and system chrome.
        services.TryAddSingleton<IDisplayPresentationController, NoopDisplayPresentationController>();

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(Log.Logger, true);
        });

        services.Configure<MireyaApiClientOptions>(options =>
        {
            options.BaseUrl =
                Environment.GetEnvironmentVariable("MIREYA_BACKEND_URL") ?? defaultBackendUrl;
        });

        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dbPath = Path.Combine(appDataPath, "Mireya", "mireya_client.db");
        var dbDirectory =
            Path.GetDirectoryName(dbPath)
            ?? throw new InvalidOperationException("The client database path has no directory.");
        Directory.CreateDirectory(dbDirectory);

        services.AddDbContext<LocalDbContext>(options =>
        {
            options.UseSqlite($"Data Source={dbPath}");
        });

        services.AddSingleton<ICredentialStorage, AvaloniaCredentialStorage>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IAssetViewFactory, TAssetViewFactory>();

        services.AddMireyaApiClient();

        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<ContentDisplayViewModel>();
        services.AddTransient<BackendSelectionViewModel>();
    }
}
