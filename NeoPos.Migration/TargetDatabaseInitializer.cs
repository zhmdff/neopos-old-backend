using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace NeoPos.Migration;

public static class TargetDatabaseInitializer
{
    public static async Task RecreatePostgresDatabaseAsync(string targetConnectionString, CancellationToken ct = default)
    {
        var builder = new NpgsqlConnectionStringBuilder(targetConnectionString);
        var dbName = builder.Database ?? throw new InvalidOperationException("Database name required.");
        builder.Database = "postgres";

        await using var conn = new NpgsqlConnection(builder.ConnectionString);
        await conn.OpenAsync(ct);

        await using (var terminate = new NpgsqlCommand($"""
            SELECT pg_terminate_backend(pid)
            FROM pg_stat_activity
            WHERE datname = @db AND pid <> pg_backend_pid()
            """, conn))
        {
            terminate.Parameters.AddWithValue("db", dbName);
            await terminate.ExecuteNonQueryAsync(ct);
        }

        var safeName = dbName.Replace("\"", "\"\"");
        await using (var drop = new NpgsqlCommand($"""DROP DATABASE IF EXISTS "{safeName}" """, conn))
            await drop.ExecuteNonQueryAsync(ct);

        await using (var create = new NpgsqlCommand($"""CREATE DATABASE "{safeName}" """, conn))
            await create.ExecuteNonQueryAsync(ct);

        Console.WriteLine($"Recreated database '{dbName}'.");
    }

    public static async Task EnsurePostgresDatabaseAsync(
        string targetConnectionString,
        string databaseName,
        CancellationToken ct = default)
    {
        var builder = new NpgsqlConnectionStringBuilder(targetConnectionString);
        var dbName = string.IsNullOrWhiteSpace(databaseName)
            ? builder.Database
            : databaseName;

        if (string.IsNullOrWhiteSpace(dbName))
            throw new InvalidOperationException("Target connection string must include Database name.");

        builder.Database = "postgres";
        await using var conn = new NpgsqlConnection(builder.ConnectionString);
        await conn.OpenAsync(ct);

        await using var existsCmd = new NpgsqlCommand(
            "SELECT 1 FROM pg_database WHERE datname = @name",
            conn);
        existsCmd.Parameters.AddWithValue("name", dbName);
        var exists = await existsCmd.ExecuteScalarAsync(ct) is not null;
        if (exists)
        {
            Console.WriteLine($"Database '{dbName}' already exists.");
            return;
        }

        var safeName = dbName.Replace("\"", "\"\"");
        await using var createCmd = new NpgsqlCommand($"""CREATE DATABASE "{safeName}" """, conn);
        await createCmd.ExecuteNonQueryAsync(ct);
        Console.WriteLine($"Created database '{dbName}'.");
    }

    public static async Task ApplyRemoteMigrationsAsync(string targetConnectionString, CancellationToken ct = default)
    {
        var currentUser = new MigrationCurrentUserService();
        var options = new DbContextOptionsBuilder<DAL.Server.Context.RemoteDbContext>()
            .UseNpgsql(targetConnectionString)
            .Options;

        await using var db = new DAL.Server.Context.RemoteDbContext(options, currentUser);
        try
        {
            await db.Database.MigrateAsync(ct);
            Console.WriteLine("RemoteDbContext migrations applied.");
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.DuplicateTable)
        {
            Console.WriteLine("RemoteDbContext migrations skipped — target schema already present.");
        }
    }
}
