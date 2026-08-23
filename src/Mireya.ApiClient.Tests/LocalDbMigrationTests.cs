using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Mireya.ApiClient.Data;

namespace Mireya.ApiClient.Tests;

public class LocalDbMigrationTests
{
    [Fact]
    public void Migrate_CreatesOptimizedClientSchema()
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

                command.CommandText =
                    "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' "
                    + "AND name IN ('Display', 'CampaignAssignment')";
                Assert.Equal(0L, command.ExecuteScalar());

                command.CommandText =
                    "SELECT COUNT(*) FROM pragma_index_list('CampaignAssets') "
                    + "WHERE name = 'IX_CampaignAssets_CampaignId_Position' AND [unique] = 1";
                Assert.Equal(1L, command.ExecuteScalar());

                command.CommandText =
                    "SELECT COUNT(*) FROM pragma_index_list('BackendInstances') "
                    + "WHERE name = 'IX_BackendInstances_IsCurrentBackend' AND [unique] = 1";
                Assert.Equal(1L, command.ExecuteScalar());

                var now = DateTime.UtcNow.ToString("O");
                command.CommandText =
                    $"INSERT INTO BackendInstances "
                    + $"(Id, BaseUrl, IsCurrentBackend, LastConnectedAt, CreatedAt) VALUES "
                    + $"('{Guid.NewGuid()}', 'https://one.example', 1, '{now}', '{now}')";
                command.ExecuteNonQuery();

                command.CommandText =
                    $"INSERT INTO BackendInstances "
                    + $"(Id, BaseUrl, IsCurrentBackend, LastConnectedAt, CreatedAt) VALUES "
                    + $"('{Guid.NewGuid()}', 'https://two.example', 1, '{now}', '{now}')";
                Assert.Throws<SqliteException>(() => command.ExecuteNonQuery());
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
