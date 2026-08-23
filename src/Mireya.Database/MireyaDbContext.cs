using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Mireya.Database.Models;

namespace Mireya.Database;

public class MireyaDbContext(DbContextOptions<MireyaDbContext> options)
    : IdentityDbContext<User>(options)
{
    public DbSet<Screen> Screens { get; set; }
    public DbSet<Asset> Assets { get; set; }
    public DbSet<Campaign> Campaigns { get; set; }
    public DbSet<CampaignAsset> CampaignAssets { get; set; }
    public DbSet<CampaignAssignment> CampaignAssignments { get; set; }
    public DbSet<AssetSyncStatus> AssetSyncStatuses { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<PlaybackEvent> PlaybackEvents { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Configure Screen entity
        builder.Entity<Screen>(entity =>
        {
            entity.HasIndex(e => e.ScreenIdentifier).IsUnique();
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => new
            {
                e.ApprovalStatus,
                e.IsActive,
                e.CreatedAt,
            });

            entity
                .HasOne<User>()
                .WithOne()
                .HasForeignKey<Screen>(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_Screens_ResolutionWidth_Positive",
                    "\"ResolutionWidth\" IS NULL OR \"ResolutionWidth\" > 0"
                );
                table.HasCheckConstraint(
                    "CK_Screens_ResolutionHeight_Positive",
                    "\"ResolutionHeight\" IS NULL OR \"ResolutionHeight\" > 0"
                );
            });
        });

        // Configure Asset entity
        builder.Entity<Asset>(entity =>
        {
            entity.HasIndex(e => e.Type);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_Assets_FileSizeBytes_NonNegative",
                    "\"FileSizeBytes\" IS NULL OR \"FileSizeBytes\" >= 0"
                );
                table.HasCheckConstraint(
                    "CK_Assets_DurationSeconds_Positive",
                    "\"DurationSeconds\" IS NULL OR \"DurationSeconds\" > 0"
                );
            });
        });

        // Configure Campaign entity
        builder.Entity<Campaign>(entity =>
        {
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.IsDefault).IsUnique().HasFilter("\"IsDefault\"");
            entity.ToTable(table =>
            {
                table.HasCheckConstraint(
                    "CK_Campaigns_DateRange",
                    "\"StartDateUtc\" IS NULL OR \"EndDateUtc\" IS NULL OR \"StartDateUtc\" <= \"EndDateUtc\""
                );
                table.HasCheckConstraint(
                    "CK_Campaigns_RecurrenceDaysMask_Range",
                    "\"RecurrenceDaysMask\" IS NULL OR \"RecurrenceDaysMask\" BETWEEN 0 AND 127"
                );
                table.HasCheckConstraint(
                    "CK_Campaigns_DailyWindow_Complete",
                    "(\"DailyStartTime\" IS NULL) = (\"DailyEndTime\" IS NULL)"
                );
            });
        });

        // Configure CampaignAsset entity
        builder.Entity<CampaignAsset>(entity =>
        {
            entity.HasIndex(e => e.AssetId);
            entity.HasIndex(e => new { e.CampaignId, e.Position }).IsUnique();
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_CampaignAssets_Position_Positive", "\"Position\" > 0");
                table.HasCheckConstraint(
                    "CK_CampaignAssets_DurationSeconds_Positive",
                    "\"DurationSeconds\" IS NULL OR \"DurationSeconds\" > 0"
                );
            });

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
            entity.HasIndex(e => e.ScreenId);
            entity.HasIndex(e => new { e.CampaignId, e.ScreenId }).IsUnique();

            entity
                .HasOne(ca => ca.Campaign)
                .WithMany(c => c.CampaignAssignments)
                .HasForeignKey(ca => ca.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(ca => ca.Screen)
                .WithMany(d => d.CampaignAssignments)
                .HasForeignKey(ca => ca.ScreenId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure AssetSyncStatus entity
        builder.Entity<AssetSyncStatus>(entity =>
        {
            entity.HasIndex(e => e.AssetId);
            entity.HasIndex(e => e.SyncState);
            entity.HasIndex(e => new { e.ScreenId, e.AssetId }).IsUnique();
            entity.ToTable(table =>
                table.HasCheckConstraint(
                    "CK_AssetSyncStatuses_Progress_Range",
                    "\"Progress\" BETWEEN 0 AND 100"
                )
            );

            entity
                .HasOne(ass => ass.Screen)
                .WithMany()
                .HasForeignKey(ass => ass.ScreenId)
                .OnDelete(DeleteBehavior.Cascade);

            entity
                .HasOne(ass => ass.Asset)
                .WithMany()
                .HasForeignKey(ass => ass.AssetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure AuditLog entity
        builder.Entity<AuditLog>(entity =>
        {
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.EntityType);
            entity.HasIndex(e => e.ActorUserId);
        });

        // Configure PlaybackEvent entity (proof of play)
        builder.Entity<PlaybackEvent>(entity =>
        {
            entity.HasIndex(e => e.PlayedAtUtc);
            entity.HasIndex(e => e.ScreenId);
            entity.HasIndex(e => e.AssetId);

            entity
                .HasOne(pe => pe.Screen)
                .WithMany()
                .HasForeignKey(pe => pe.ScreenId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
