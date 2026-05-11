using Microsoft.EntityFrameworkCore;
using Mireya.Database;
using Npgsql;

namespace Mireya.Api.Startup;

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
            var npgsqlBuilder = new NpgsqlConnectionStringBuilder(connectionString);

            // Only override SSL mode if not already specified in the connection string
            if (!connectionString.Contains("SSL Mode", StringComparison.OrdinalIgnoreCase)
                && !connectionString.Contains("SslMode", StringComparison.OrdinalIgnoreCase))
            {
                npgsqlBuilder.SslMode = SslMode.Prefer;
            }

            services.AddDbContext<MireyaDbContext>(options =>
                options.UseNpgsql(
                    npgsqlBuilder.ConnectionString,
                    x => x.MigrationsAssembly(Provider.Postgres.Assembly)
                )
            );
        }
        else
        {
            throw new InvalidOperationException(
                $"Unsupported database provider: '{provider}'. Supported providers: '{Provider.Sqlite.Name}', '{Provider.Postgres.Name}'.");
        }
    }
}
