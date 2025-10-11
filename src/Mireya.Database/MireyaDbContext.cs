using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Mireya.Database.Models;

namespace Mireya.Database;

public class MireyaDbContext(DbContextOptions<MireyaDbContext> options) : IdentityDbContext<User>(options)
{
    public DbSet<Display> Displays { get; set; }
    public DbSet<Asset> Assets { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Display entity
        modelBuilder.Entity<Display>(entity =>
        {
            entity.HasIndex(e => e.ScreenIdentifier).IsUnique();
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.ApprovalStatus);
        });

        // Configure Asset entity
        modelBuilder.Entity<Asset>(entity =>
        {
            entity.HasIndex(e => e.Type);
        });
    }

    public static async Task InitializeAsync(MireyaDbContext db)
    {
        // Generate initial data for development
    }
}