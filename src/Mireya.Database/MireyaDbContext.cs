using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Mireya.Database.Models;

namespace Mireya.Database;

public class MireyaDbContext(DbContextOptions<MireyaDbContext> options)
    : IdentityDbContext<User>(options)
{
    public DbSet<Display> Displays { get; set; }
    public DbSet<Asset> Assets { get; set; }
    public DbSet<Campaign> Campaigns { get; set; }
    public DbSet<CampaignAsset> CampaignAssets { get; set; }
    public DbSet<CampaignAssignment> CampaignAssignments { get; set; }
    public DbSet<AssetSyncStatus> AssetSyncStatuses { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Configure Display entity
        builder.Entity<Display>(entity =>
        {
            entity.HasIndex(e => e.ScreenIdentifier).IsUnique();
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.ApprovalStatus);
        });

        // Configure Asset entity
        builder.Entity<Asset>(entity =>
        {
            entity.HasIndex(e => e.Type);
        });

        // Configure Campaign entity
        builder.Entity<Campaign>(entity =>
        {
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.CreatedAt);
        });

        // Configure CampaignAsset entity
        builder.Entity<CampaignAsset>(entity =>
        {
            entity.HasIndex(e => e.CampaignId);
            entity.HasIndex(e => e.AssetId);
            entity.HasIndex(e => new { e.CampaignId, e.Position }).IsUnique();

            entity
                .HasOne(ca => ca.Campaign)
                .WithMany(c => c.CampaignAssets)
                .HasForeignKey(ca => ca.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(ca => ca.Asset)
                .WithMany()
                .HasForeignKey(ca => ca.AssetId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent asset deletion if used in campaigns
        });

        // Configure CampaignAssignment entity
        builder.Entity<CampaignAssignment>(entity =>
        {
            entity.HasIndex(e => e.CampaignId);
            entity.HasIndex(e => e.DisplayId);
            entity.HasIndex(e => new { e.CampaignId, e.DisplayId }).IsUnique();

            entity
                .HasOne(ca => ca.Campaign)
                .WithMany(c => c.CampaignAssignments)
                .HasForeignKey(ca => ca.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(ca => ca.Display)
                .WithMany(d => d.CampaignAssignments)
                .HasForeignKey(ca => ca.DisplayId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure AssetSyncStatus entity
        builder.Entity<AssetSyncStatus>(entity =>
        {
            entity.HasIndex(e => e.DisplayId);
            entity.HasIndex(e => e.AssetId);
            entity.HasIndex(e => e.SyncState);
            entity.HasIndex(e => new { e.DisplayId, e.AssetId }).IsUnique();

            entity
                .HasOne(ass => ass.Display)
                .WithMany()
                .HasForeignKey(ass => ass.DisplayId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(ass => ass.Asset)
                .WithMany()
                .HasForeignKey(ass => ass.AssetId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    public static async Task InitializeAsync(MireyaDbContext db)
    {
        // Generate initial data for development
    }
}
