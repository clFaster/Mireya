using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Mireya.Database.Models;

namespace Mireya.Client.Avalonia.Data;

/// <summary>
/// Local client database context - stores downloaded campaign and asset data for offline use
/// Supports multiple backend instances with encrypted credential storage
/// </summary>
public class LocalDbContext : DbContext
{
    // Backend tracking
    public DbSet<BackendInstance> BackendInstances { get; set; } = null!;
    public DbSet<BackendCredential> BackendCredentials { get; set; } = null!;
    public DbSet<BackendAsset> BackendAssets { get; set; } = null!;
    public DbSet<BackendCampaign> BackendCampaigns { get; set; } = null!;
    
    // Server models (reused)
    public DbSet<Asset> Assets { get; set; } = null!;
    public DbSet<Campaign> Campaigns { get; set; } = null!;
    public DbSet<CampaignAsset> CampaignAssets { get; set; } = null!;
    
    // Download tracking
    public DbSet<DownloadedAsset> DownloadedAssets { get; set; } = null!;

    public LocalDbContext(DbContextOptions<LocalDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure BackendInstance
        modelBuilder.Entity<BackendInstance>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.BaseUrl).IsUnique();
            entity.Property(e => e.BaseUrl).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(200);
        });

        // Configure BackendCredential
        modelBuilder.Entity<BackendCredential>(entity =>
        {
            entity.HasKey(e => e.BackendInstanceId);
            
            entity.HasOne<BackendInstance>()
                .WithMany()
                .HasForeignKey(e => e.BackendInstanceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure BackendAsset - maps assets to backends
        modelBuilder.Entity<BackendAsset>(entity =>
        {
            entity.HasKey(e => new { e.BackendInstanceId, e.AssetId });
            
            entity.HasOne<BackendInstance>()
                .WithMany()
                .HasForeignKey(e => e.BackendInstanceId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(ba => ba.Asset)
                .WithMany()
                .HasForeignKey(ba => ba.AssetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure BackendCampaign - maps campaigns to backends
        modelBuilder.Entity<BackendCampaign>(entity =>
        {
            entity.HasKey(e => new { e.BackendInstanceId, e.CampaignId });
            
            entity.HasOne<BackendInstance>()
                .WithMany()
                .HasForeignKey(e => e.BackendInstanceId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(bc => bc.Campaign)
                .WithMany()
                .HasForeignKey(bc => bc.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Asset - using server model
        modelBuilder.Entity<Asset>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Type);
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Source).HasMaxLength(2000);
        });

        // Configure Campaign - using server model
        modelBuilder.Entity<Campaign>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name);
            entity.Property(e => e.Name).HasMaxLength(200);
        });

        // Configure CampaignAsset - using server model
        modelBuilder.Entity<CampaignAsset>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CampaignId);
            entity.HasIndex(e => e.AssetId);
            entity.HasIndex(e => new { e.CampaignId, e.Position });

            entity.HasOne(ca => ca.Campaign)
                .WithMany(c => c.CampaignAssets)
                .HasForeignKey(ca => ca.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ca => ca.Asset)
                .WithMany()
                .HasForeignKey(ca => ca.AssetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure DownloadedAsset - client-specific tracking
        modelBuilder.Entity<DownloadedAsset>(entity =>
        {
            entity.HasKey(e => new { e.BackendInstanceId, e.AssetId });
            entity.HasIndex(e => e.IsDownloaded);
            entity.Property(e => e.LocalPath).HasMaxLength(500);
            entity.Property(e => e.FileExtension).HasMaxLength(10);
            
            entity.HasOne<BackendInstance>()
                .WithMany()
                .HasForeignKey(e => e.BackendInstanceId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

/// <summary>
/// Represents a backend instance the client has connected to
/// </summary>
public class BackendInstance
{
    public Guid Id { get; set; }
    
    public string BaseUrl { get; set; } = string.Empty;
    
    public string? Name { get; set; }
    
    public bool IsCurrentBackend { get; set; }
    
    public DateTime LastConnectedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Stores credentials for a backend instance
/// Tokens are encrypted at rest using Windows DPAPI
/// </summary>
public class BackendCredential
{
    public Guid BackendInstanceId { get; set; }
    
    public string? Username { get; set; }
    
    // Encrypted with DPAPI - ProtectedData.Protect/Unprotect
    public byte[]? EncryptedAccessToken { get; set; }
    
    public byte[]? EncryptedRefreshToken { get; set; }
    
    public DateTime? TokenExpiresAt { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Helper properties (not mapped to DB) - automatically encrypt/decrypt
    [NotMapped]
    public string? AccessToken
    {
        get => EncryptedAccessToken != null 
            ? Encoding.UTF8.GetString(DecryptData(EncryptedAccessToken))
            : null;
        set => EncryptedAccessToken = value != null
            ? EncryptData(Encoding.UTF8.GetBytes(value))
            : null;
    }
    
    [NotMapped]
    public string? RefreshToken
    {
        get => EncryptedRefreshToken != null
            ? Encoding.UTF8.GetString(DecryptData(EncryptedRefreshToken))
            : null;
        set => EncryptedRefreshToken = value != null
            ? EncryptData(Encoding.UTF8.GetBytes(value))
            : null;
    }
    
    private static byte[] EncryptData(byte[] data)
    {
        if (OperatingSystem.IsWindows())
        {
            return ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
        }
        // On non-Windows platforms, store as-is (in production, use platform-specific encryption)
        return data;
    }
    
    private static byte[] DecryptData(byte[] data)
    {
        if (OperatingSystem.IsWindows())
        {
            return ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
        }
        // On non-Windows platforms, data is not encrypted
        return data;
    }
}

/// <summary>
/// Maps assets to backend instances
/// </summary>
public class BackendAsset
{
    public Guid BackendInstanceId { get; set; }
    
    public Guid AssetId { get; set; }
    
    public Asset Asset { get; set; } = null!;
    
    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Maps campaigns to backend instances
/// </summary>
public class BackendCampaign
{
    public Guid BackendInstanceId { get; set; }
    
    public Guid CampaignId { get; set; }
    
    public Campaign Campaign { get; set; } = null!;
    
    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Client-specific model to track download status of assets per backend
/// </summary>
public class DownloadedAsset
{
    public Guid BackendInstanceId { get; set; }
    
    public Guid AssetId { get; set; }
    
    public string? LocalPath { get; set; }
    
    public string? FileExtension { get; set; }
    
    public bool IsDownloaded { get; set; }
    
    public DateTime? DownloadedAt { get; set; }
    
    public DateTime LastCheckedAt { get; set; } = DateTime.UtcNow;
}
