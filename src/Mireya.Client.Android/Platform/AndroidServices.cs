using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mireya.ApiClient;
using Mireya.ApiClient.Data;
using Mireya.ApiClient.Options;
using Mireya.ApiClient.Services;
using Mireya.Client.Avalonia.AndroidTv.Platform;
using Mireya.Client.Avalonia.Platform;
using Mireya.Client.Avalonia.Services;
using Mireya.Client.Avalonia.ViewModels;
using Serilog;

namespace Mireya.Client.Avalonia.AndroidTv;

/// <summary>
///     Composition root for the Android TV head. Wires the shared services from
///     <c>Mireya.Client.Core</c> together with the Android-specific implementations
///     (System WebView / libVLC asset renderers) and reuses the platform-neutral
///     credential/settings storage and the local SQLite store from Core.
/// </summary>
public static class AndroidServices
{
    public static IServiceProvider Build()
    {
        var services = new ServiceCollection();

        // Register app settings singleton — DI resolves IServiceScopeFactory automatically
        services.AddSingleton<AppSettings>();

        // Add Serilog logging (the shared console sink is routed to logcat on Android)
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(Log.Logger, true);
        });

        // Configure API client options. The default backend URL can be preconfigured for
        // unattended/kiosk deployments via the MIREYA_BACKEND_URL environment variable;
        // it is otherwise overridden by the URL the user enters on first start.
        services.Configure<MireyaApiClientOptions>(options =>
        {
            options.BaseUrl =
                Environment.GetEnvironmentVariable("MIREYA_BACKEND_URL")
                ?? App.DefaultBackendUrl;
        });

        // Configure local SQLite database. On Android, LocalApplicationData resolves to the
        // application's private files directory, so the same path logic as desktop applies.
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dbPath = Path.Combine(appDataPath, "Mireya", "mireya_client.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        services.AddDbContext<LocalDbContext>(options => { options.UseSqlite($"Data Source={dbPath}"); });

        // Register platform-specific implementations (these must be registered
        // before AddMireyaApiClient which depends on them). Credential and settings
        // storage are platform-neutral and shared with the desktop head via Core.
        services.AddSingleton<ICredentialStorage, AvaloniaCredentialStorage>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IAssetViewFactory, AndroidAssetViewFactory>();

        // Register all API client services (token management, auth, SignalR, sync, etc.)
        services.AddMireyaApiClient();

        // Register ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<ContentDisplayViewModel>();
        services.AddTransient<BackendSelectionViewModel>();

        return services.BuildServiceProvider();
    }
}
