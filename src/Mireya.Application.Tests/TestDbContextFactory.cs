using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Mireya.Database;
using Mireya.Database.Models;

namespace Mireya.Application.Tests;

/// <summary>
///     Creates an isolated <see cref="MireyaDbContext" /> backed by an in-memory SQLite database.
///     The connection is kept open for the lifetime of the returned context so the schema persists.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDatabase()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<MireyaDbContext>().UseSqlite(_connection).Options;

        Context = new MireyaDbContext(options);
        Context.Database.EnsureCreated();
    }

    public MireyaDbContext Context { get; }

    public void AddScreen(Screen screen)
    {
        if (screen.UserId is not null)
        {
            Context.Users.Add(
                new User
                {
                    Id = screen.UserId,
                    UserName = screen.UserId,
                    NormalizedUserName = screen.UserId.ToUpperInvariant(),
                    CreatedAt = DateTime.UtcNow,
                }
            );
        }

        Context.Screens.Add(screen);
    }

    public MireyaDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<MireyaDbContext>().UseSqlite(_connection).Options;
        return new MireyaDbContext(options);
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
