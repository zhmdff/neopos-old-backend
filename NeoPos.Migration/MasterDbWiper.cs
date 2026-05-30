using Microsoft.Extensions.Configuration;
using Npgsql;

namespace NeoPos.Migration;

public enum MasterDbWipeStrategy
{
    /// <summary>Local Postgres: DROP DATABASE + CREATE DATABASE.</summary>
    DropDatabase,
    /// <summary>Neon / hosted Postgres: DROP SCHEMA public CASCADE (cannot drop the DB itself).</summary>
    DropSchema,
}

public sealed class MasterDbWipeOptions
{
    public required string ConnectionString { get; init; }
    public MasterDbWipeStrategy Strategy { get; init; } = MasterDbWipeStrategy.DropDatabase;
    public bool DryRun { get; init; }
    public string? ConfirmPhrase { get; init; }
}

public static class MasterDbWiper
{
    public static async Task<int> RunAsync(MasterDbWipeOptions options, CancellationToken ct = default)
    {
        var builder = new NpgsqlConnectionStringBuilder(options.ConnectionString);
        var dbName = builder.Database
                     ?? throw new InvalidOperationException("Connection string must include Database=...");

        var targetLabel = options.Strategy == MasterDbWipeStrategy.DropSchema ? "Neon / cloud" : "local master";
        Console.WriteLine($"{targetLabel} wipe → PostgreSQL \"{dbName}\" on {builder.Host}");
        Console.WriteLine();

        await PrintCurrentStatsAsync(options.ConnectionString, dbName, ct);

        if (options.DryRun)
        {
            PrintDryRunPlan(dbName, options.Strategy);
            Console.WriteLine();
            Console.WriteLine($"To execute: --confirm {dbName}");
            return 0;
        }

        if (!string.Equals(options.ConfirmPhrase, dbName, StringComparison.Ordinal))
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("Refusing wipe — confirmation mismatch.");
            Console.Error.WriteLine($"Pass --confirm {dbName} to delete ALL data.");
            return 1;
        }

        Console.WriteLine();
        Console.WriteLine("Wiping…");

        if (options.Strategy == MasterDbWipeStrategy.DropSchema)
            await WipeSchemaAsync(options.ConnectionString, builder.Username, ct);
        else
        {
            await TargetDatabaseInitializer.RecreatePostgresDatabaseAsync(options.ConnectionString, ct);
            await TargetDatabaseInitializer.ApplyRemoteMigrationsAsync(options.ConnectionString, ct);
        }

        Console.WriteLine();
        Console.WriteLine($"Done. \"{dbName}\" is empty.");
        return 0;
    }

    private static void PrintDryRunPlan(string dbName, MasterDbWipeStrategy strategy)
    {
        Console.WriteLine();
        Console.WriteLine("DRY RUN — would:");
        if (strategy == MasterDbWipeStrategy.DropSchema)
        {
            Console.WriteLine("  1. DROP SCHEMA public CASCADE (all tables + data)");
            Console.WriteLine("  2. CREATE SCHEMA public + restore grants");
            Console.WriteLine("  3. Apply EF migrations (empty schema)");
        }
        else
        {
            Console.WriteLine($"  1. Terminate connections to \"{dbName}\"");
            Console.WriteLine($"  2. DROP DATABASE \"{dbName}\"");
            Console.WriteLine($"  3. CREATE DATABASE \"{dbName}\"");
            Console.WriteLine("  4. Apply EF migrations (empty schema)");
        }
    }

    private static async Task WipeSchemaAsync(string connectionString, string? username, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        await ExecAsync(conn, "DROP SCHEMA IF EXISTS public CASCADE;", ct);
        await ExecAsync(conn, "CREATE SCHEMA public;", ct);

        if (!string.IsNullOrWhiteSpace(username))
        {
            var safeUser = username.Replace("\"", "\"\"");
            await ExecAsync(conn, $"GRANT ALL ON SCHEMA public TO \"{safeUser}\";", ct);
        }

        await ExecAsync(conn, "GRANT ALL ON SCHEMA public TO public;", ct);
        await TargetDatabaseInitializer.ApplyRemoteMigrationsAsync(connectionString, ct);
    }

    private static async Task ExecAsync(NpgsqlConnection conn, string sql, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task PrintCurrentStatsAsync(string connectionString, string dbName, CancellationToken ct)
    {
        try
        {
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync(ct);

            var hasCompanies = await TableExistsAsync(conn, "Companies", ct);
            if (!hasCompanies)
            {
                Console.WriteLine("No NeoPos schema yet (empty or not migrated).");
                return;
            }

            var companies = await ScalarAsync(conn, """SELECT COUNT(*) FROM "Companies" """, ct);
            var users = await ScalarAsync(conn, """SELECT COUNT(*) FROM "Users" """, ct);
            var products = await ScalarAsync(conn, """SELECT COUNT(*) FROM "Products" """, ct);
            var orders = await ScalarAsync(conn, """SELECT COUNT(*) FROM "OrderHeaders" """, ct);

            Console.WriteLine("Current contents:");
            Console.WriteLine($"  Companies:     {companies}");
            Console.WriteLine($"  Users:         {users}");
            Console.WriteLine($"  Products:      {products}");
            Console.WriteLine($"  OrderHeaders:  {orders}");

            await using var list = new NpgsqlCommand(
                """
                SELECT "Id", "NameAz", COALESCE("TenantKey", '(null)'), "IsDeleted"
                FROM "Companies"
                ORDER BY "CreatedAt" DESC
                LIMIT 25
                """,
                conn);
            await using var r = await list.ExecuteReaderAsync(ct);
            var any = false;
            while (await r.ReadAsync(ct))
            {
                if (!any)
                {
                    Console.WriteLine();
                    Console.WriteLine("Tenants (up to 25):");
                    any = true;
                }

                var deleted = r.GetBoolean(3) ? " [deleted]" : "";
                Console.WriteLine($"  {r.GetGuid(0)} | {r.GetString(1)} | {r.GetString(2)}{deleted}");
            }

            if (!any)
                Console.WriteLine("  (no companies)");
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.InvalidCatalogName)
        {
            Console.WriteLine($"Database \"{dbName}\" does not exist.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not read stats ({ex.Message}).");
        }
    }

    private static async Task<bool> TableExistsAsync(NpgsqlConnection conn, string table, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(
            """
            SELECT 1 FROM information_schema.tables
            WHERE table_schema = 'public' AND table_name = @t
            LIMIT 1
            """,
            conn);
        cmd.Parameters.AddWithValue("t", table);
        return await cmd.ExecuteScalarAsync(ct) is not null;
    }

    private static async Task<long> ScalarAsync(NpgsqlConnection conn, string sql, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(sql, conn);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is null or DBNull ? 0 : Convert.ToInt64(result);
    }
}

public static class NeonConnectionResolver
{
    public static string? Resolve(IConfiguration config)
    {
        var direct = config["Migration:NeonConnection"];
        if (!string.IsNullOrWhiteSpace(direct))
            return direct.Trim();

        foreach (var path in FindWebApiAppsettingsPaths())
        {
            if (!File.Exists(path))
                continue;

            var web = new ConfigurationBuilder()
                .AddJsonFile(path, optional: false)
                .Build();

            var cs = web.GetConnectionString("RemotePostgres");
            if (!string.IsNullOrWhiteSpace(cs))
                return cs.Trim();
        }

        return null;
    }

    private static IEnumerable<string> FindWebApiAppsettingsPaths()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in CandidateRoots())
        {
            var path = Path.Combine(root, "NeoPos.WebAPI", "appsettings.json");
            if (seen.Add(path))
                yield return path;
        }
    }

    private static IEnumerable<string> CandidateRoots()
    {
        yield return Directory.GetCurrentDirectory();

        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8 && !string.IsNullOrEmpty(dir); i++)
        {
            yield return dir;
            yield return Path.Combine(dir, "..");
            dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }
    }
}
