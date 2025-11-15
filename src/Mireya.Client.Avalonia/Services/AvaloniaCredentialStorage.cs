using System;
using System.IO;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Mireya.ApiClient.Models;

namespace Mireya.Client.Avalonia.Services;

/// <summary>
/// Avalonia implementation of credential storage using encrypted JSON files
/// </summary>
public class AvaloniaCredentialStorage : ICredentialStorage
{
    private readonly string _credentialsFilePath;
    private const string CredentialsFileName = "credentials.dat";
    private const string AppFolderName = "Mireya";

    public AvaloniaCredentialStorage()
    {
        // Get platform-specific app data folder
        var appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataFolder, AppFolderName);
        
        // Ensure directory exists
        Directory.CreateDirectory(appFolder);
        
        _credentialsFilePath = Path.Combine(appFolder, CredentialsFileName);
    }

    public async Task SaveCredentialsAsync(Credentials credentials)
    {
        try
        {
            var json = JsonSerializer.Serialize(credentials);
            var encrypted = ProtectData(json);
            await File.WriteAllBytesAsync(_credentialsFilePath, encrypted);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CredentialStorage] Error saving credentials: {ex.Message}");
            throw new InvalidOperationException("Failed to save credentials securely", ex);
        }
    }

    public async Task<Credentials?> GetCredentialsAsync()
    {
        try
        {
            if (!File.Exists(_credentialsFilePath))
            {
                return null;
            }

            var encrypted = await File.ReadAllBytesAsync(_credentialsFilePath);
            var json = UnprotectData(encrypted);
            return JsonSerializer.Deserialize<Credentials>(json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CredentialStorage] Error reading credentials: {ex.Message}");
            return null;
        }
    }

    public async Task DeleteCredentialsAsync()
    {
        try
        {
            if (File.Exists(_credentialsFilePath))
            {
                await Task.Run(() => File.Delete(_credentialsFilePath));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CredentialStorage] Error deleting credentials: {ex.Message}");
            throw new InvalidOperationException("Failed to delete credentials", ex);
        }
    }

    public async Task<bool> HasCredentialsAsync()
    {
        await Task.CompletedTask; // Make async for consistency
        return File.Exists(_credentialsFilePath);
    }

    /// <summary>
    /// Encrypt data using DPAPI (Windows) or basic encryption (other platforms)
    /// </summary>
    private static byte[] ProtectData(string data)
    {
        var bytes = Encoding.UTF8.GetBytes(data);
        
        // Use DPAPI on Windows for secure encryption
        if (OperatingSystem.IsWindows())
        {
            return System.Security.Cryptography.ProtectedData.Protect(bytes, null, System.Security.Cryptography.DataProtectionScope.CurrentUser);
        }
        
        // For non-Windows platforms, use a simple XOR encryption
        // Note: This is not as secure as DPAPI. Consider using platform-specific secure storage
        // or a cross-platform encryption library in production
        return XorEncrypt(bytes);
    }

    /// <summary>
    /// Decrypt data using DPAPI (Windows) or basic decryption (other platforms)
    /// </summary>
    private static string UnprotectData(byte[] encryptedData)
    {
        byte[] bytes;
        
        if (OperatingSystem.IsWindows())
        {
            bytes = System.Security.Cryptography.ProtectedData.Unprotect(encryptedData, null, System.Security.Cryptography.DataProtectionScope.CurrentUser);
        }
        else
        {
            bytes = XorEncrypt(encryptedData); // XOR is symmetric
        }
        
        return Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// Simple XOR encryption for non-Windows platforms
    /// Note: Not cryptographically secure - for demonstration only
    /// </summary>
    private static byte[] XorEncrypt(byte[] data)
    {
        // Generate a simple key from machine-specific data
        var key = GetMachineKey();
        var result = new byte[data.Length];
        
        for (int i = 0; i < data.Length; i++)
        {
            result[i] = (byte)(data[i] ^ key[i % key.Length]);
        }
        
        return result;
    }

    /// <summary>
    /// Generate a machine-specific key for XOR encryption
    /// </summary>
    private static byte[] GetMachineKey()
    {
        var machineId = Environment.MachineName + Environment.UserName;
        return SHA256.HashData(Encoding.UTF8.GetBytes(machineId));
    }
}
