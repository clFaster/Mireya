using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Mireya.ApiClient.Data;

namespace Mireya.ApiClient.Tests;

public class LocalDbMigrationTests
{
    [Fact]
    public void Migrate_AddsBackendScopedPasswordColumn()
    {
        var databasePath = Path.Combine(
            Path.GetTempPath(),
            $"mireya-client-tests-{Guid.NewGuid():N}.db"
        );

        try
        {
            var options = new DbContextOptionsBuilder<LocalDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;

            using (var db = new LocalDbContext(options))
                db.Database.Migrate();

            using (var connection = new SqliteConnection($"Data Source={databasePath}"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText =
                    "SELECT COUNT(*) FROM pragma_table_info('BackendCredentials') "
                    + "WHERE name = 'EncryptedPassword'";

                Assert.Equal(1L, command.ExecuteScalar());
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath))
                File.Delete(databasePath);
        }
    }
}
