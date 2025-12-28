using Mireya.Database.Sqlite;

namespace Mireya.Api;

public record Provider(string Name, string Assembly)
{
    public static readonly Provider Sqlite = new(
        nameof(Sqlite),
        typeof(Marker).Assembly.GetName().Name!
    );

    public static readonly Provider Postgres = new(
        nameof(Postgres),
        typeof(Database.Postgres.Marker).Assembly.GetName().Name!
    );
}
