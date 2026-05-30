using Npgsql;

namespace NeoPos.Migration;

public sealed class CompanySourceHit
{
    public required string DatabaseName { get; init; }
    public required string ConnectionString { get; init; }
    public required Guid CompanyId { get; init; }
    public required string NameAz { get; init; }
    public string? TenantKey { get; init; }
    public bool IsLegacySchema { get; init; }
}

public static class CompanySourceLocator
{
    public static async Task<CompanySourceHit?> FindLocalAsync(
        Guid companyId,
        string adminSampleConnectionString,
        IEnumerable<string>? preferDatabaseNames = null,
        string? excludeDatabaseName = null,
        CancellationToken ct = default)
    {
        var builder = new NpgsqlConnectionStringBuilder(adminSampleConnectionString);
        builder.Database = "postgres";

        await using var conn = new NpgsqlConnection(builder.ConnectionString);
        await conn.OpenAsync(ct);

        await using var dbs = new NpgsqlCommand(
            "SELECT datname FROM pg_database WHERE datistemplate = false ORDER BY datname", conn);
        await using var dr = await dbs.ExecuteReaderAsync(ct);
        var names = new List<string>();
        while (await dr.ReadAsync(ct))
            names.Add(dr.GetString(0));
        await dr.CloseAsync();

        var preferred = (preferDatabaseNames ?? []).ToList();
        var ordered = names
            .Where(n => !string.Equals(n, excludeDatabaseName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(n =>
            {
                var idx = preferred.FindIndex(p => string.Equals(p, n, StringComparison.OrdinalIgnoreCase));
                return idx >= 0 ? idx : 1000;
            })
            .ThenBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var db in ordered)
        {
            var hit = await TryReadCompanyAsync(builder, db, companyId, ct);
            if (hit != null)
                return hit;
        }

        return null;
    }

    public static async Task<CompanySourceHit?> FindInDatabaseAsync(
        Guid companyId,
        string connectionString,
        CancellationToken ct = default)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.Database))
            throw new InvalidOperationException("Connection string must include Database=...");

        return await TryReadCompanyAsync(builder, builder.Database, companyId, ct);
    }

    private static async Task<CompanySourceHit?> TryReadCompanyAsync(
        NpgsqlConnectionStringBuilder builder,
        string databaseName,
        Guid companyId,
        CancellationToken ct)
    {
        builder.Database = databaseName;
        try
        {
            await using var conn = new NpgsqlConnection(builder.ConnectionString);
            await conn.OpenAsync(ct);

            await using var tables = new NpgsqlCommand(
                """SELECT 1 FROM information_schema.tables WHERE table_schema='public' AND table_name='Companies' LIMIT 1""",
                conn);
            if (await tables.ExecuteScalarAsync(ct) is null)
                return null;

            var hasTenantKey = false;
            await using (var col = new NpgsqlCommand(
                """SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='Companies' AND column_name='TenantKey' LIMIT 1""",
                conn))
            {
                hasTenantKey = await col.ExecuteScalarAsync(ct) is not null;
            }

            var sql = hasTenantKey
                ? """SELECT "Id", "NameAz", "TenantKey" FROM "Companies" WHERE "Id" = @id"""
                : """SELECT "Id", "NameAz", NULL FROM "Companies" WHERE "Id" = @id""";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", companyId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct))
                return null;

            var tenantKey = r.IsDBNull(2) ? null : r.GetString(2);
            return new CompanySourceHit
            {
                DatabaseName = databaseName,
                ConnectionString = builder.ConnectionString,
                CompanyId = r.GetGuid(0),
                NameAz = r.GetString(1),
                TenantKey = string.IsNullOrWhiteSpace(tenantKey) ? null : tenantKey.Trim(),
                IsLegacySchema = !hasTenantKey || string.IsNullOrWhiteSpace(tenantKey),
            };
        }
        catch
        {
            return null;
        }
    }
}
