using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Mireya.Client.Avalonia.Services;

/// <summary>
/// Implementation of settings service using JSON file storage
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly string _settingsFilePath;
    private const string SettingsFileName = "settings.json";
    private const string AppFolderName = "Mireya";

    public SettingsService()
    {
        // Get platform-specific app data folder
        var appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataFolder, AppFolderName);
        
        // Ensure directory exists
        Directory.CreateDirectory(appFolder);
        
        _settingsFilePath = Path.Combine(appFolder, SettingsFileName);
    }

    public async Task<string?> GetBackendUrlAsync()
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(_settingsFilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json);
            return settings?.BackendUrl;
        }
        catch (Exception ex)
        {
            // PoC: In production, use proper logging framework (e.g., Serilog, NLog)
            // For now, write to console for debugging purposes
            Console.WriteLine($"[SettingsService] Error reading settings: {ex.Message}");
            return null;
        }
    }

    public async Task SaveBackendUrlAsync(string url)
    {
        try
        {
            var settings = new AppSettings { BackendUrl = url };
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            
            await File.WriteAllTextAsync(_settingsFilePath, json);
        }
        catch (Exception ex)
        {
            // PoC: In production, use proper logging framework (e.g., Serilog, NLog)
            // For now, write to console and rethrow to notify UI
            Console.WriteLine($"[SettingsService] Error saving settings: {ex.Message}");
            throw;
        }
    }

    public bool IsValidUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        // Basic URL validation
        return Uri.TryCreate(url, UriKind.Absolute, out var uriResult) 
               && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
    }

    /// <summary>
    /// Internal settings data structure
    /// </summary>
    private class AppSettings
    {
        public string? BackendUrl { get; set; }
    }
}
