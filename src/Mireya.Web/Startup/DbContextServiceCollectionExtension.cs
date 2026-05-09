using Microsoft.EntityFrameworkCore;
using Mireya.Database;
using Npgsql;

namespace Mireya.Web.Startup;

public static class DbContextServiceCollectionExtension
{
    public static void AddMireyaDbContext(this IServiceCollection services, IConfiguration config)
    {
        var provider = config.GetValue("provider", Provider.Sqlite.Name);

        if (provider == Provider.Sqlite.Name)
            services.AddDbContext<MireyaDbContext>(options =>
                options.UseSqlite(
                    config.GetConnectionString(Provider.Sqlite.Name)!,
                    x => x.MigrationsAssembly(Provider.Sqlite.Assembly)
                )
            );
        else if (provider == Provider.Postgres.Name)
        {
            var connectionString = config.GetConnectionString(Provider.Postgres.Name)!;
            var npgsqlBuilder = new NpgsqlConnectionStringBuilder(connectionString)
            {
                SslMode = SslMode.Disable,
            };

            services.AddDbContext<MireyaDbContext>(options =>
                options.UseNpgsql(
                    npgsqlBuilder.ConnectionString,
                    x => x.MigrationsAssembly(Provider.Postgres.Assembly)
                )
            );
        }
    }
}
