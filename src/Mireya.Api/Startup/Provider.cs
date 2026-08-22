using Mireya.Database.Sqlite;

namespace Mireya.Api.Startup;

public record Provider(string Name, string Assembly)
{
    public static readonly Provider Sqlite = new(
        nameof(Sqlite),
        typeof(IMarker).Assembly.GetName().Name
            ?? throw new InvalidOperationException("The SQLite provider assembly has no name.")
    );

    public static readonly Provider Postgres = new(
        nameof(Postgres),
        typeof(Database.Postgres.IMarker).Assembly.GetName().Name
            ?? throw new InvalidOperationException("The PostgreSQL provider assembly has no name.")
    );
}
