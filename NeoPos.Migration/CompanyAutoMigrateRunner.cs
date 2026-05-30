using Npgsql;

namespace NeoPos.Migration;

public sealed class CompanyAutoMigrateOptions
{
    public required Guid CompanyId { get; init; }
    public required string TargetConnectionString { get; init; }
    public string? PreferredSourceConnectionString { get; init; }
    /// <summary>When set, copy from this database only (e.g. neopos_sync). Skips scanning other DBs.</summary>
    public string? SourceConnectionString { get; init; }
    public IEnumerable<string>? PreferSourceDatabases { get; init; }
    public string? TenantKeyOverride { get; init; }
    public bool RecreateTargetDatabase { get; init; }
    public bool DryRun { get; init; }
}

public sealed class CompanyAutoMigrateResult
{
    public bool Success { get; set; }
    public string? SourceDatabase { get; set; }
    public string? CompanyName { get; set; }
    public string? TenantKey { get; set; }
    public string? TargetDatabase { get; set; }
    public bool UsedLegacyImport { get; set; }
    public List<string> Messages { get; } = [];
    public List<string> Errors { get; } = [];
}

public sealed class CompanyAutoMigrateRunner
{
    public async Task<CompanyAutoMigrateResult> RunAsync(
        CompanyAutoMigrateOptions options,
        CancellationToken ct = default)
    {
        var result = new CompanyAutoMigrateResult();
        var targetBuilder = new NpgsqlConnectionStringBuilder(options.TargetConnectionString);
        result.TargetDatabase = targetBuilder.Database;
        var hostedTarget = IsHostedPostgres(targetBuilder);

        CompanySourceHit? hit;
        if (!string.IsNullOrWhiteSpace(options.SourceConnectionString))
        {
            var srcBuilder = new NpgsqlConnectionStringBuilder(options.SourceConnectionString);
            Console.WriteLine($"Reading company {options.CompanyId:D} from \"{srcBuilder.Database}\"…");
            hit = await CompanySourceLocator.FindInDatabaseAsync(
                options.CompanyId, options.SourceConnectionString, ct);
            if (hit == null)
            {
                result.Errors.Add(
                    $"Company {options.CompanyId:D} not found in \"{srcBuilder.Database}\".");
                return result;
            }
        }
        else
        {
            var probeCs = options.PreferredSourceConnectionString ?? options.TargetConnectionString;
            var preferDbs = options.PreferSourceDatabases?.ToList()
                            ?? new List<string> { "neopos_db", "neondb" };

            if (!string.IsNullOrWhiteSpace(options.PreferredSourceConnectionString))
            {
                var srcB = new NpgsqlConnectionStringBuilder(options.PreferredSourceConnectionString);
                if (!string.IsNullOrWhiteSpace(srcB.Database))
                    preferDbs.Insert(0, srcB.Database);
            }

            Console.WriteLine($"Looking for company {options.CompanyId:D} on local PostgreSQL…");

            hit = await CompanySourceLocator.FindLocalAsync(
                options.CompanyId,
                probeCs,
                preferDbs,
                excludeDatabaseName: targetBuilder.Database,
                ct);

            if (hit == null)
            {
                result.Errors.Add(
                    $"Company {options.CompanyId:D} not found in any local PostgreSQL database (excluding target '{targetBuilder.Database}').");
                return result;
            }
        }

        result.SourceDatabase = hit.DatabaseName;
        result.CompanyName = hit.NameAz;
        result.Messages.Add($"Source: {hit.DatabaseName} ({hit.NameAz})");

        var tenantKey = !string.IsNullOrWhiteSpace(options.TenantKeyOverride)
            ? options.TenantKeyOverride.Trim()
            : !string.IsNullOrWhiteSpace(hit.TenantKey)
                ? hit.TenantKey!
                : TenantKeyGenerator.FromCompanyName(hit.NameAz, hit.CompanyId);

        result.TenantKey = tenantKey;
        result.Messages.Add($"TenantKey: {tenantKey}");

        if (options.DryRun)
        {
            result.Messages.Add($"Dry run — would migrate using {(hit.IsLegacySchema ? "legacy import" : "full EF copy")}.");
            result.Success = true;
            return result;
        }

        if (options.RecreateTargetDatabase && !hostedTarget)
        {
            await TargetDatabaseInitializer.RecreatePostgresDatabaseAsync(options.TargetConnectionString, ct);
            result.Messages.Add($"Target database '{targetBuilder.Database}' recreated.");
        }
        else if (!hostedTarget)
        {
            await TargetDatabaseInitializer.EnsurePostgresDatabaseAsync(options.TargetConnectionString, databaseName: "", ct);
        }

        await TargetDatabaseInitializer.ApplyRemoteMigrationsAsync(options.TargetConnectionString, ct);
        result.Messages.Add("Target schema ready.");

        if (hit.IsLegacySchema)
        {
            result.UsedLegacyImport = true;
            var srcBuilder = new NpgsqlConnectionStringBuilder(hit.ConnectionString);
            await LegacyCrossDatabaseMigrator.RunAsync(new LegacyCrossDatabaseMigrator.Options
            {
                TargetConnectionString = options.TargetConnectionString,
                SourceDatabase = srcBuilder.Database ?? hit.DatabaseName,
                SourceUser = srcBuilder.Username ?? "postgres",
                SourcePassword = srcBuilder.Password ?? "",
                CompanyId = options.CompanyId,
                TenantKey = tenantKey,
            }, ct);
        }
        else
        {
            var full = await new FullCompanyMigrationRunner().RunAsync(new FullCompanyMigrationOptions
            {
                SourceConnectionString = hit.ConnectionString,
                TargetConnectionString = options.TargetConnectionString,
                CompanyId = options.CompanyId,
                TenantKey = tenantKey,
                DryRun = false,
            }, ct);

            foreach (var step in full.Steps)
                result.Messages.Add(step);
            foreach (var w in full.Warnings)
                result.Messages.Add($"WARN: {w}");
            foreach (var e in full.Errors)
                result.Errors.Add(e);

            if (full.Errors.Count > 0)
                return result;
        }

        result.Messages.Add("Migration completed.");
        result.Success = true;
        return result;
    }

    private static bool IsHostedPostgres(NpgsqlConnectionStringBuilder builder) =>
        builder.Host?.Contains("neon.tech", StringComparison.OrdinalIgnoreCase) == true;
}
