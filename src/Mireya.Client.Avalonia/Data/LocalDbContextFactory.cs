using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Mireya.Client.Avalonia.Data;

/// <summary>
///     Design-time factory for LocalDbContext - used by EF migrations
/// </summary>
public class LocalDbContextFactory : IDesignTimeDbContextFactory<LocalDbContext>
{
    public LocalDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<LocalDbContext>();

        // Use a dummy connection string for migrations
        // The actual database path will be set at runtime
        optionsBuilder.UseSqlite("Data Source=mireya_client.db");

        return new LocalDbContext(optionsBuilder.Options);
    }
}
