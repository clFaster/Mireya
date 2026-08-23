using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Mireya.Database.Sqlite;

public sealed class MireyaDesignTimeDbContextFactory : IDesignTimeDbContextFactory<MireyaDbContext>
{
    public MireyaDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<MireyaDbContext>()
            .UseSqlite(
                "Data Source=mireya-design.db",
                sqlite => sqlite.MigrationsAssembly(typeof(IMarker).Assembly.FullName)
            )
            .Options;
        return new MireyaDbContext(options);
    }
}
