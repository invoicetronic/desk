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

        await AddMissingColumnsAsync(conn, config);
        await CreateDataProtectionTableAsync(conn, config);
        await EncryptPlaintextApiKeysAsync(conn, config, scope.ServiceProvider);
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

    private static async Task AddMissingColumnsAsync(
        System.Data.Common.DbConnection conn, DeskConfig config)
    {
        string[] extraColumns =
        [
            "StripeCustomerId", "SubscriptionStatus",
            "CompanyName", "TaxId", "Address", "City", "State", "ZipCode",
            "Country", "PecMail", "CodiceDestinatario"
        ];

        await using var cmd = conn.CreateCommand();

        if (config.Database.Provider is "pgsql")
        {
            var checks = string.Join("\n", extraColumns.Select(c =>
                $"""
                            IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='AspNetUsers' AND column_name='{c}') THEN
                                ALTER TABLE "AspNetUsers" ADD COLUMN "{c}" TEXT;
                            END IF;
                """));
            cmd.CommandText = $"""
                DO $$
                BEGIN
                {checks}
                END $$;
                """;
        }
        else
        {
            cmd.CommandText = "PRAGMA table_info(AspNetUsers)";
            var columns = new HashSet<string>();
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                    columns.Add(reader.GetString(1));
            }

            var alterStatements = extraColumns
                .Where(c => !columns.Contains(c))
                .Select(c => $"ALTER TABLE AspNetUsers ADD COLUMN {c} TEXT")
                .ToList();

            cmd.CommandText = string.Join("; ", alterStatements);
        }

        if (!string.IsNullOrEmpty(cmd.CommandText))
            await cmd.ExecuteNonQueryAsync();
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
        System.Data.Common.DbConnection conn, DeskConfig config, IServiceProvider services)
    {
        var protector = services.GetRequiredService<ApiKeyProtector>();

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

        await using var tx = await conn.BeginTransactionAsync();
        foreach (var (id, key) in toEncrypt)
        {
            await using var updateCmd = conn.CreateCommand();
            updateCmd.Transaction = (System.Data.Common.DbTransaction)tx;
            updateCmd.CommandText = config.Database.Provider is "pgsql"
                ? """UPDATE "AspNetUsers" SET "ApiKey" = @key WHERE "Id" = @id"""
                : "UPDATE AspNetUsers SET ApiKey = @key WHERE Id = @id";

            var pKey = updateCmd.CreateParameter();
            pKey.ParameterName = "@key";
            pKey.Value = protector.Protect(key);
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
