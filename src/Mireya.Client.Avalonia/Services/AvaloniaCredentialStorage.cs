using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Mireya.ApiClient.Models;
using Mireya.ApiClient.Services;

namespace Mireya.Client.Avalonia.Services;

/// <summary>
///     Avalonia implementation of credential storage using encrypted JSON files
/// </summary>
public class AvaloniaCredentialStorage : ICredentialStorage
{
    private const string CredentialsFileName = "credentials.dat";
    private const string AppFolderName = "Mireya";
    private readonly string _credentialsFilePath;
    private readonly ILogger<AvaloniaCredentialStorage> _logger;

    public AvaloniaCredentialStorage(ILogger<AvaloniaCredentialStorage> logger)
    {
        _logger = logger;

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
            _logger.LogError(ex, "Error saving credentials");
            throw new InvalidOperationException("Failed to save credentials securely", ex);
        }
    }

    public async Task<Credentials?> GetCredentialsAsync()
    {
        try
        {
            if (!File.Exists(_credentialsFilePath))
                return null;

            var encrypted = await File.ReadAllBytesAsync(_credentialsFilePath);
            var json = UnprotectData(encrypted);
            return JsonSerializer.Deserialize<Credentials>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading credentials");
            return null;
        }
    }

    public async Task DeleteCredentialsAsync()
    {
        try
        {
            if (File.Exists(_credentialsFilePath))
                await Task.Run(() => File.Delete(_credentialsFilePath));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting credentials");
            throw new InvalidOperationException("Failed to delete credentials", ex);
        }
    }

    public async Task<bool> HasCredentialsAsync()
    {
        await Task.CompletedTask; // Make async for consistency
        return File.Exists(_credentialsFilePath);
    }

    /// <summary>
    ///     Encrypt data using DPAPI (Windows) or AES-GCM (other platforms)
    /// </summary>
    private static byte[] ProtectData(string data)
    {
        var bytes = Encoding.UTF8.GetBytes(data);

        // Use DPAPI on Windows for secure encryption
        if (OperatingSystem.IsWindows())
            return ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);

        // For non-Windows platforms, use AES-GCM with a machine-derived key
        return AesGcmEncrypt(bytes);
    }

    /// <summary>
    ///     Decrypt data using DPAPI (Windows) or AES-GCM (other platforms)
    /// </summary>
    private static string UnprotectData(byte[] encryptedData)
    {
        byte[] bytes;

        if (OperatingSystem.IsWindows())
            bytes = ProtectedData.Unprotect(encryptedData, null, DataProtectionScope.CurrentUser);
        else
            bytes = AesGcmDecrypt(encryptedData);

        return Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    ///     Encrypt data using AES-GCM with a machine-derived key.
    ///     Output format: [12-byte nonce][16-byte tag][ciphertext]
    /// </summary>
    private static byte[] AesGcmEncrypt(byte[] plaintext)
    {
        var key = GetMachineKey();
        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize]; // 12 bytes
        RandomNumberGenerator.Fill(nonce);

        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize]; // 16 bytes

        using var aes = new AesGcm(key, AesGcm.TagByteSizes.MaxSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        // Combine: nonce + tag + ciphertext
        var result = new byte[nonce.Length + tag.Length + ciphertext.Length];
        nonce.CopyTo(result, 0);
        tag.CopyTo(result, nonce.Length);
        ciphertext.CopyTo(result, nonce.Length + tag.Length);
        return result;
    }

    /// <summary>
    ///     Decrypt data using AES-GCM with a machine-derived key.
    ///     Input format: [12-byte nonce][16-byte tag][ciphertext]
    /// </summary>
    private static byte[] AesGcmDecrypt(byte[] encryptedData)
    {
        var key = GetMachineKey();
        const int nonceSize = 12;
        const int tagSize = 16;

        var nonce = encryptedData.AsSpan(0, nonceSize);
        var tag = encryptedData.AsSpan(nonceSize, tagSize);
        var ciphertext = encryptedData.AsSpan(nonceSize + tagSize);

        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(key, tagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);
        return plaintext;
    }

    /// <summary>
    ///     Generate a machine-specific key for XOR encryption
    /// </summary>
    private static byte[] GetMachineKey()
    {
        var machineId = Environment.MachineName + Environment.UserName;
        return SHA256.HashData(Encoding.UTF8.GetBytes(machineId));
    }
}
