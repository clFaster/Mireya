using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Mireya.Database.Postgres;

public sealed class MireyaDesignTimeDbContextFactory : IDesignTimeDbContextFactory<MireyaDbContext>
{
    public MireyaDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<MireyaDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=mireya_design;Username=mireya;Password=mireya",
                postgres => postgres.MigrationsAssembly(typeof(IMarker).Assembly.FullName)
            )
            .Options;
        return new MireyaDbContext(options);
    }
}
