using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Mireya.ApiClient.Data;

namespace Mireya.Client.Avalonia.Services;

/// <summary>
/// Application-wide settings singleton backed by the local SQLite database.
///
/// Lifecycle:
///   1. Registered as AddSingleton&lt;AppSettings&gt;() in ConfigureServices.
///   2. Call LoadAsync() once at startup (after migrations) to populate the properties.
///   3. Call SaveAsync() whenever the user changes a value.
///
/// Default values (used when a key is absent from the DB) are written back on
/// the first Load, so every key is seeded on the very first run.
/// </summary>
public sealed class AppSettings
{
    private readonly IServiceScopeFactory _scopeFactory;

    // ──────────────────────────────────────────────────────────────
    // Setting properties
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Launch the main window in fullscreen (WindowState.FullScreen, no title bar).
    /// </summary>
    public bool Fullscreen { get; set; }

    /// <summary>
    /// Skip the server-selection screen: wait 5 s, check the last-used server is
    /// online, and connect automatically. Takes effect on next application launch.
    /// </summary>
    public bool AutoStart { get; set; }

    // ──────────────────────────────────────────────────────────────
    // Immediate-apply callbacks (wired by App.axaml.cs after window creation)
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Invoked by the settings UI after saving Fullscreen so the window state can
    /// be toggled without a restart.  Wired in App.axaml.cs.
    /// </summary>
    public Action<bool>? ApplyFullscreen { get; set; }

    // ──────────────────────────────────────────────────────────────

    public AppSettings(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Reads settings from the database.  Any key that does not yet exist is
    /// inserted with its default value so subsequent saves always perform UPDATEs.
    /// </summary>
    public async Task LoadAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalDbContext>();

        Fullscreen = await GetBoolAsync(db, "Fullscreen", false);
        AutoStart = await GetBoolAsync(db, "AutoStart", false);

        // Remove the retired overlay preference during upgrade. Screen Info is now an
        // on-demand page, so retaining this value would be misleading dead state.
        var retiredScreenInfoSetting = await db.ClientSettings.FindAsync("HideScreenInfo");
        if (retiredScreenInfoSetting != null)
        {
            db.ClientSettings.Remove(retiredScreenInfoSetting);
            await db.SaveChangesAsync();
        }
    }

    /// <summary>Persists the current property values to the database.</summary>
    public async Task SaveAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LocalDbContext>();

        await SetValueAsync(db, "Fullscreen", Fullscreen.ToString());
        await SetValueAsync(db, "AutoStart", AutoStart.ToString());
    }

    // ──────────────────────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────────────────────

    private static async Task<bool> GetBoolAsync(LocalDbContext db, string key, bool defaultValue)
    {
        var row = await db.ClientSettings.FindAsync(key);
        if (row == null)
        {
            // Seed the default so the table always has a row for this key
            db.ClientSettings.Add(new ClientSetting { Key = key, Value = defaultValue.ToString() });
            await db.SaveChangesAsync();
            return defaultValue;
        }

        return bool.TryParse(row.Value, out var v) ? v : defaultValue;
    }

    private static async Task SetValueAsync(LocalDbContext db, string key, string value)
    {
        var row = await db.ClientSettings.FindAsync(key);
        if (row == null)
            db.ClientSettings.Add(new ClientSetting { Key = key, Value = value });
        else
            row.Value = value;

        await db.SaveChangesAsync();
    }
}
