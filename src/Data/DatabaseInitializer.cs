using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Desk.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, DeskConfig config)
    {
        if (config.IsStandalone)
            return;

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DeskDbContext>();

        EnsureSqliteDirectory(db, config);
        await db.Database.EnsureCreatedAsync();

        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();

        await CreateDataProtectionTableAsync(conn, config);

        await conn.CloseAsync();
        await EncryptPlaintextApiKeysAsync(db, config, scope.ServiceProvider);
    }

    private static void EnsureSqliteDirectory(DeskDbContext db, DeskConfig config)
    {
        if (config.Database.Provider is "pgsql")
            return;

        var connString = db.Database.GetConnectionString();
        if (connString is null)
            return;

        var builder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connString);
        if (!string.IsNullOrEmpty(builder.DataSource) && builder.DataSource != ":memory:")
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(builder.DataSource));
            if (dir is not null) Directory.CreateDirectory(dir);
        }
    }

    private static async Task CreateDataProtectionTableAsync(
        System.Data.Common.DbConnection conn, DeskConfig config)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = config.Database.Provider is "pgsql"
            ? """
                CREATE TABLE IF NOT EXISTS "DataProtectionKeys" (
                    "Id" SERIAL PRIMARY KEY,
                    "FriendlyName" TEXT,
                    "Xml" TEXT
                );
                """
            : """
                CREATE TABLE IF NOT EXISTS DataProtectionKeys (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    FriendlyName TEXT,
                    Xml TEXT
                );
                """;

        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task EncryptPlaintextApiKeysAsync(
        DeskDbContext db, DeskConfig config, IServiceProvider services)
    {
        var protector = services.GetRequiredService<ApiKeyProtector>();

        var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();

        await using var readCmd = conn.CreateCommand();
        readCmd.CommandText = config.Database.Provider is "pgsql"
            ? """SELECT "Id", "ApiKey" FROM "AspNetUsers" WHERE "ApiKey" IS NOT NULL"""
            : "SELECT Id, ApiKey FROM AspNetUsers WHERE ApiKey IS NOT NULL";

        var toEncrypt = new List<(string id, string key)>();
        using (var reader = await readCmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var id = reader.GetString(0);
                var key = reader.GetString(1);
                if (!protector.IsEncrypted(key))
                    toEncrypt.Add((id, key));
            }
        }

        if (toEncrypt.Count == 0)
            return;

        // Close raw connection so DataProtection can write its keys via EF Core
        await conn.CloseAsync();

        // Encrypt all keys (may trigger DataProtection key creation on first call)
        var encrypted = toEncrypt.Select(x => (x.id, encrypted: protector.Protect(x.key))).ToList();

        // Reopen connection for updates
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();
        foreach (var (id, key) in encrypted)
        {
            await using var updateCmd = conn.CreateCommand();
            updateCmd.Transaction = (System.Data.Common.DbTransaction)tx;
            updateCmd.CommandText = config.Database.Provider is "pgsql"
                ? """UPDATE "AspNetUsers" SET "ApiKey" = @key WHERE "Id" = @id"""
                : "UPDATE AspNetUsers SET ApiKey = @key WHERE Id = @id";

            var pKey = updateCmd.CreateParameter();
            pKey.ParameterName = "@key";
            pKey.Value = key;
            updateCmd.Parameters.Add(pKey);

            var pId = updateCmd.CreateParameter();
            pId.ParameterName = "@id";
            pId.Value = id;
            updateCmd.Parameters.Add(pId);

            await updateCmd.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
        Console.WriteLine($"info: Encrypted {toEncrypt.Count} plaintext API key(s).");
    }
}
