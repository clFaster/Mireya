using Mireya.Database.Sqlite;

namespace Mireya.Api.Startup;

public record Provider(string Name, string Assembly)
{
    public static readonly Provider Sqlite = new(
        nameof(Sqlite),
        typeof(IMarker).Assembly.GetName().Name!
    );

    public static readonly Provider Postgres = new(
        nameof(Postgres),
        typeof(Database.Postgres.IMarker).Assembly.GetName().Name!
    );
}
