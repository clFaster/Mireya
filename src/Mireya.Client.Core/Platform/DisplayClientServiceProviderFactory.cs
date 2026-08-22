using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
    public static IServiceProvider Build<TAssetViewFactory>(
        string defaultBackendUrl,
        bool supportsFullscreen
    )
        where TAssetViewFactory : class, IAssetViewFactory
    {
        var services = new ServiceCollection();
        services.AddDisplayClientServices<TAssetViewFactory>(defaultBackendUrl, supportsFullscreen);
        return services.BuildServiceProvider();
    }

    private static IServiceCollection AddDisplayClientServices<TAssetViewFactory>(
        this IServiceCollection services,
        string defaultBackendUrl,
        bool supportsFullscreen
    )
        where TAssetViewFactory : class, IAssetViewFactory
    {
        services.AddSingleton(new ClientPlatformCapabilities { SupportsFullscreen = supportsFullscreen });
        services.AddSingleton<AppSettings>();

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
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

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

        return services;
    }
}
