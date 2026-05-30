using Microsoft.Extensions.Configuration;
using NeoPos.Migration;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

static void PrintHelp()
{
    Console.WriteLine("""
        NeoPos legacy / master → target migration

        Usage:
          dotnet run --project NeoPos.Migration -- [options]

        Options (override appsettings.migration.json):
          --source <connectionString>     Source PostgreSQL (Neon / legacy)
          --source-schema <name>          Default: public (legacy user-only mode)
          --target <postgres|sqlite>        Destination database
          --target-connection <cs>        Target connection string
          --company-id <guid>             Company UUID to migrate
          --tenant-key <string>           TenantKey (optional for --full; required for user-only)
          --auto-migrate                  Find company locally + migrate to neopos_sync (see migrate-company.ps1)
          --push-neon                     Copy company FROM neopos_sync TO Neon (see push-company-to-neon.ps1)
          --sync-connection <cs>          Source for --push-neon (default: Migration:SyncConnection / TargetConnection)
          --recreate-target-db            DROP + CREATE target database, then migrate schema
          --legacy-import                 Import from legacy DB on same server (postgres_fdw)
          --full                          Copy full tenant (catalog, orders, users, …)
          --dry-run                       Preview only
          --update-tenant-key             Set TenantKey on existing company row (user-only)
          --scan-local                    Search local Postgres DBs for --company-id
          --probe-company                 List company in source DB (or recent companies)
          --wipe-master                   Wipe LOCAL master DB (neopos_sync): DROP DATABASE
          --wipe-neon                     Wipe Neon cloud neondb: DROP SCHEMA (see wipe-neon-db.ps1)
          --confirm <database-name>       Required: neondb or neopos_sync
          --help

        Examples:
          # Preview Neon wipe (RemotePostgres from WebAPI appsettings)
          dotnet run --project NeoPos.Migration -- --wipe-neon --dry-run

          # Empty Neon neondb completely
          .\migrations\wipe-neon-db.ps1 -Confirm neondb

          # Wipe local neopos_sync only
          .\migrations\wipe-master-db.ps1 -Confirm neopos_sync

          # Push from local neopos_sync to Neon
          .\migrations\push-company-to-neon.ps1 1122fb73-fe84-4e31-b726-56f4bf678757

          # Auto-migrate one company (legacy neopos_db → neopos_sync)
          dotnet run --project NeoPos.Migration -- --auto-migrate --company-id <guid>

          # Or pass the GUID as the only argument
          dotnet run --project NeoPos.Migration -- --auto-migrate 1122fb73-fe84-4e31-b726-56f4bf678757

        See migrations/README.md for full guide.
        """);
}

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.migration.json", optional: true)
    .AddEnvironmentVariables(prefix: "NEOPOS_MIGRATION_")
    .Build();

var argsMap = ParseArgs(args);
if (argsMap.ContainsKey("help"))
{
    PrintHelp();
    return 0;
}

string? Get(string key, string configKey)
{
    if (argsMap.TryGetValue(key, out var v)) return v;
    return config[configKey];
}

var sourceCs = Get("source", "Migration:SourcePostgres");
var sourceSchema = Get("source-schema", "Migration:SourceSchema") ?? "public";
var targetProvider = Get("target", "Migration:TargetProvider") ?? "postgres";
var targetCs = Get("target-connection", "Migration:TargetConnection");
var companyIdStr = Get("company-id", "Migration:CompanyId");
var tenantKey = Get("tenant-key", "Migration:TenantKey");
var dryRun = argsMap.ContainsKey("dry-run") || config.GetValue("Migration:DryRun", false);
var updateTenantKey = argsMap.ContainsKey("update-tenant-key") || config.GetValue("Migration:UpdateTenantKey", false);
var initTargetDb = argsMap.ContainsKey("init-target-db") || config.GetValue("Migration:InitTargetDatabase", false);
var recreateTargetDb = argsMap.ContainsKey("recreate-target-db") || config.GetValue("Migration:RecreateTargetDatabase", false);
var legacyImport = argsMap.ContainsKey("legacy-import") || config.GetValue("Migration:LegacyImport", false);
var full = argsMap.ContainsKey("full") || config.GetValue("Migration:Full", false);
var autoMigrate = argsMap.ContainsKey("auto-migrate") || config.GetValue("Migration:AutoMigrate", false);
var pushNeon = argsMap.ContainsKey("push-neon") || config.GetValue("Migration:PushNeon", false);
var wipeMaster = argsMap.ContainsKey("wipe-master") || config.GetValue("Migration:WipeMaster", false);
var wipeNeon = argsMap.ContainsKey("wipe-neon") || config.GetValue("Migration:WipeNeon", false);
var confirmPhrase = Get("confirm", "Migration:Confirm");

var positionalCompanyId = args.FirstOrDefault(a =>
    !a.StartsWith("--", StringComparison.Ordinal) && Guid.TryParse(a, out _));

if (wipeMaster || wipeNeon)
{
    string? masterCs;
    MasterDbWipeStrategy strategy;

    if (wipeNeon)
    {
        masterCs = NeonConnectionResolver.Resolve(config);
        strategy = MasterDbWipeStrategy.DropSchema;
        if (string.IsNullOrWhiteSpace(masterCs))
        {
            Console.Error.WriteLine("Neon wipe: set Migration:NeonConnection or ConnectionStrings:RemotePostgres in NeoPos.WebAPI/appsettings.json.");
            return 1;
        }
    }
    else
    {
        masterCs = targetCs ?? config["Migration:TargetConnection"];
        strategy = MasterDbWipeStrategy.DropDatabase;
        if (string.IsNullOrWhiteSpace(masterCs))
        {
            Console.Error.WriteLine("Local wipe requires Migration:TargetConnection in appsettings.migration.json.");
            return 1;
        }
    }

    return await MasterDbWiper.RunAsync(new MasterDbWipeOptions
    {
        ConnectionString = masterCs,
        Strategy = strategy,
        DryRun = dryRun,
        ConfirmPhrase = string.IsNullOrWhiteSpace(confirmPhrase) ? null : confirmPhrase.Trim(),
    });
}

if (autoMigrate)
{
    var idStr = argsMap.TryGetValue("company-id", out var fromFlag) ? fromFlag : positionalCompanyId;
    if (string.IsNullOrWhiteSpace(idStr) || !Guid.TryParse(idStr, out var autoCompanyId))
    {
        Console.Error.WriteLine("Auto-migrate requires a company GUID (--company-id or positional argument).");
        return 1;
    }

    var targetForAuto = targetCs ?? config["Migration:TargetConnection"];
    if (string.IsNullOrWhiteSpace(targetForAuto))
    {
        Console.Error.WriteLine("Auto-migrate requires Migration:TargetConnection in appsettings.migration.json.");
        return 1;
    }

    var runner = new CompanyAutoMigrateRunner();
    var autoResult = await runner.RunAsync(new CompanyAutoMigrateOptions
    {
        CompanyId = autoCompanyId,
        TargetConnectionString = targetForAuto,
        PreferredSourceConnectionString = sourceCs,
        PreferSourceDatabases = config.GetSection("Migration:PreferSourceDatabases").Get<string[]>(),
        TenantKeyOverride = string.IsNullOrWhiteSpace(tenantKey) ? null : tenantKey.Trim(),
        RecreateTargetDatabase = recreateTargetDb,
        DryRun = dryRun,
    });

    Console.WriteLine();
    foreach (var m in autoResult.Messages)
        Console.WriteLine(m);
    foreach (var e in autoResult.Errors)
        Console.WriteLine($"ERROR: {e}");

    if (autoResult.Success)
    {
        Console.WriteLine();
        Console.WriteLine($"Done. Set NeoPos:TenantKey=\"{autoResult.TenantKey}\" on tenant POS for this company.");
    }

    return autoResult.Success ? 0 : 2;
}

if (pushNeon)
{
    var idStr = argsMap.TryGetValue("company-id", out var pushFromFlag) ? pushFromFlag : positionalCompanyId ?? companyIdStr;
    if (string.IsNullOrWhiteSpace(idStr) || !Guid.TryParse(idStr, out var pushCompanyId))
    {
        Console.Error.WriteLine("push-neon requires a company GUID (--company-id or positional argument).");
        return 1;
    }

    var syncCs = Get("sync-connection", "Migration:SyncConnection")
                 ?? config["Migration:TargetConnection"];
    var neonCs = Get("target-connection", "Migration:NeonConnection")
                 ?? NeonConnectionResolver.Resolve(config);

    if (string.IsNullOrWhiteSpace(syncCs))
    {
        Console.Error.WriteLine("push-neon requires Migration:SyncConnection or Migration:TargetConnection (neopos_sync).");
        return 1;
    }

    if (string.IsNullOrWhiteSpace(neonCs))
    {
        Console.Error.WriteLine("push-neon requires Neon connection (RemotePostgres in WebAPI appsettings or Migration:NeonConnection).");
        return 1;
    }

    var syncDb = new Npgsql.NpgsqlConnectionStringBuilder(syncCs).Database;
    var neonDb = new Npgsql.NpgsqlConnectionStringBuilder(neonCs).Database;
    Console.WriteLine($"Push {pushCompanyId:D}: {syncDb} → {neonDb}");

    var pushRunner = new CompanyAutoMigrateRunner();
    var pushResult = await pushRunner.RunAsync(new CompanyAutoMigrateOptions
    {
        CompanyId = pushCompanyId,
        SourceConnectionString = syncCs,
        TargetConnectionString = neonCs,
        TenantKeyOverride = string.IsNullOrWhiteSpace(tenantKey) ? null : tenantKey.Trim(),
        RecreateTargetDatabase = false,
        DryRun = dryRun,
    });

    Console.WriteLine();
    foreach (var m in pushResult.Messages)
        Console.WriteLine(m);
    foreach (var e in pushResult.Errors)
        Console.WriteLine($"ERROR: {e}");

    if (pushResult.Success)
    {
        Console.WriteLine();
        Console.WriteLine($"Done. Tenant \"{pushResult.TenantKey}\" is on Neon. Set NeoPos:TenantKey on POS terminals.");
    }

    return pushResult.Success ? 0 : 2;
}

if (argsMap.ContainsKey("scan-local"))
{
    await ScanLocalPostgresForCompanyAsync(companyIdStr ?? "", targetCs ?? Get("target-connection", "Migration:TargetConnection") ?? "");
    return 0;
}

if (argsMap.ContainsKey("probe-company"))
{
    if (string.IsNullOrWhiteSpace(sourceCs))
    {
        Console.Error.WriteLine("Missing --source or Migration:SourcePostgres.");
        return 1;
    }
    await ProbeCompanyAsync(sourceCs, companyIdStr ?? "");
    return 0;
}

if (string.IsNullOrWhiteSpace(sourceCs) || string.IsNullOrWhiteSpace(targetCs) ||
    string.IsNullOrWhiteSpace(companyIdStr))
{
    Console.Error.WriteLine("Missing required settings. Provide appsettings.migration.json or CLI args. Use --help.");
    return 1;
}

if (!Guid.TryParse(companyIdStr, out var companyId))
{
    Console.Error.WriteLine($"Invalid --company-id: {companyIdStr}");
    return 1;
}

if ((initTargetDb || recreateTargetDb) && !dryRun)
{
    try
    {
        if (recreateTargetDb)
            await TargetDatabaseInitializer.RecreatePostgresDatabaseAsync(targetCs);
        else
            await TargetDatabaseInitializer.EnsurePostgresDatabaseAsync(targetCs, databaseName: "");
        await TargetDatabaseInitializer.ApplyRemoteMigrationsAsync(targetCs);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Target DB init failed: {ex.Message}");
        return 1;
    }
}

if (legacyImport && !dryRun)
{
    if (string.IsNullOrWhiteSpace(targetCs) || string.IsNullOrWhiteSpace(tenantKey))
    {
        Console.Error.WriteLine("--legacy-import requires target connection and Migration:TenantKey.");
        return 1;
    }

    var srcBuilder = new Npgsql.NpgsqlConnectionStringBuilder(sourceCs ?? "");
    Console.WriteLine($"Legacy import | {srcBuilder.Database} → {new Npgsql.NpgsqlConnectionStringBuilder(targetCs).Database} | Company={companyId} | TenantKey={tenantKey}");
    try
    {
        await LegacyCrossDatabaseMigrator.RunAsync(new LegacyCrossDatabaseMigrator.Options
        {
            TargetConnectionString = targetCs,
            SourceDatabase = srcBuilder.Database ?? "neopos_db",
            SourceUser = srcBuilder.Username ?? "postgres",
            SourcePassword = srcBuilder.Password ?? "",
            CompanyId = companyId,
            TenantKey = tenantKey.Trim(),
        });
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex.ToString());
        return 1;
    }
}

if (full)
{
    Console.WriteLine($"NeoPos FULL migration | Company={companyId} | Target=postgres | DryRun={dryRun}");
    try
    {
        var runner = new FullCompanyMigrationRunner();
        var result = await runner.RunAsync(new FullCompanyMigrationOptions
        {
            SourceConnectionString = sourceCs,
            TargetConnectionString = targetCs,
            CompanyId = companyId,
            TenantKey = string.IsNullOrWhiteSpace(tenantKey) ? null : tenantKey.Trim(),
            DryRun = dryRun,
        });

        Console.WriteLine();
        foreach (var step in result.Steps)
            Console.WriteLine(step);
        foreach (var w in result.Warnings)
            Console.WriteLine($"WARN: {w}");
        foreach (var e in result.Errors)
            Console.WriteLine($"ERROR: {e}");

        return result.Errors.Count > 0 ? 2 : 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex.ToString());
        return 1;
    }
}

if (string.IsNullOrWhiteSpace(tenantKey))
{
    Console.Error.WriteLine("--tenant-key is required for user-only migration (or use --full).");
    return 1;
}

var options = new UserMigrationOptions
{
    SourceConnectionString = sourceCs,
    SourceSchema = sourceSchema,
    TargetConnectionString = targetCs,
    TargetProvider = targetProvider,
    CompanyId = companyId,
    TenantKey = tenantKey.Trim(),
    DryRun = dryRun,
    UpdateTenantKey = updateTenantKey
};

Console.WriteLine($"NeoPos migration | Company={companyId} | TenantKey={tenantKey} | Target={targetProvider} | DryRun={dryRun}");

try
{
    var runner = new UserMigrationRunner();
    var result = await runner.RunAsync(options);

    Console.WriteLine();
    Console.WriteLine($"Company: +{result.CompaniesInserted} inserted, {result.CompaniesSkipped} skipped, {result.CompaniesUpdated} updated");
    Console.WriteLine($"Roles:   +{result.RolesInserted} inserted, {result.RolesSkipped} skipped");
    Console.WriteLine($"Users:   +{result.UsersInserted} inserted, {result.UsersSkipped} skipped, {result.UsersRejected} rejected");

    foreach (var w in result.Warnings)
        Console.WriteLine($"WARN: {w}");
    foreach (var e in result.Errors)
        Console.WriteLine($"ERROR: {e}");

    return result.Errors.Count > 0 ? 2 : 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.ToString());
    return 1;
}

static Dictionary<string, string> ParseArgs(string[] argv)
{
    var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < argv.Length; i++)
    {
        var a = argv[i];
        if (!a.StartsWith("--", StringComparison.Ordinal))
            continue;

        var key = a[2..];
        if (key is "dry-run" or "update-tenant-key" or "help" or "init-target-db" or "recreate-target-db" or "full" or "probe-company" or "scan-local" or "legacy-import" or "auto-migrate" or "wipe-master" or "wipe-neon" or "push-neon")
        {
            map[key] = "true";
            continue;
        }

        if (i + 1 < argv.Length && !argv[i + 1].StartsWith("--", StringComparison.Ordinal))
        {
            map[key] = argv[++i];
        }
    }

    return map;
}

static async Task ProbeCompanyAsync(string sourceCs, string companyIdStr)
{
    await using var conn = new Npgsql.NpgsqlConnection(sourceCs);
    await conn.OpenAsync();
    if (Guid.TryParse(companyIdStr, out var id))
    {
        await using var cmd = new Npgsql.NpgsqlCommand(
            """SELECT "Id", "NameAz", "TenantKey" FROM "Companies" WHERE "Id" = @id""", conn);
        cmd.Parameters.AddWithValue("id", id);
        await using var r = await cmd.ExecuteReaderAsync();
        if (await r.ReadAsync())
        {
            Console.WriteLine($"FOUND: {r.GetGuid(0)} | {r.GetString(1)} | TenantKey={(r.IsDBNull(2) ? "(null)" : r.GetString(2))}");
            return;
        }
    }

    Console.WriteLine("Company not found. Recent companies in source:");
    await using var list = new Npgsql.NpgsqlCommand(
        """SELECT "Id", "NameAz", "TenantKey" FROM "Companies" WHERE NOT "IsDeleted" ORDER BY "CreatedAt" DESC LIMIT 20""", conn);
    await using var lr = await list.ExecuteReaderAsync();
    while (await lr.ReadAsync())
        Console.WriteLine($"  {lr.GetGuid(0)} | {lr.GetString(1)} | {(lr.IsDBNull(2) ? "(null)" : lr.GetString(2))}");
}

static async Task ScanLocalPostgresForCompanyAsync(string companyIdStr, string sampleCs)
{
    if (!Guid.TryParse(companyIdStr, out var id))
    {
        Console.Error.WriteLine("Provide --company-id for scan-local.");
        return;
    }

    var builder = new Npgsql.NpgsqlConnectionStringBuilder(sampleCs);
    builder.Database = "postgres";
    var adminCs = builder.ConnectionString;

    await using var conn = new Npgsql.NpgsqlConnection(adminCs);
    await conn.OpenAsync();

    await using var dbs = new Npgsql.NpgsqlCommand(
        "SELECT datname FROM pg_database WHERE datistemplate = false ORDER BY datname", conn);
    await using var dr = await dbs.ExecuteReaderAsync();
    var names = new List<string>();
    while (await dr.ReadAsync())
        names.Add(dr.GetString(0));
    await dr.CloseAsync();

    foreach (var db in names)
    {
        builder.Database = db;
        try
        {
            await using var c2 = new Npgsql.NpgsqlConnection(builder.ConnectionString);
            await c2.OpenAsync();
            await using var tables = new Npgsql.NpgsqlCommand(
                """SELECT 1 FROM information_schema.tables WHERE table_schema='public' AND table_name='Companies' LIMIT 1""", c2);
            if (await tables.ExecuteScalarAsync() is null) continue;

            var hasTenantKey = false;
            await using (var col = new Npgsql.NpgsqlCommand(
                """SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='Companies' AND column_name='TenantKey' LIMIT 1""", c2))
            {
                hasTenantKey = await col.ExecuteScalarAsync() is not null;
            }

            var sql = hasTenantKey
                ? """SELECT "Id", "NameAz", "TenantKey" FROM "Companies" WHERE "Id" = @id"""
                : """SELECT "Id", "NameAz", NULL FROM "Companies" WHERE "Id" = @id""";
            await using var cmd = new Npgsql.NpgsqlCommand(sql, c2);
            cmd.Parameters.AddWithValue("id", id);
            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                var tk = r.IsDBNull(2) ? "(null)" : r.GetString(2);
                Console.WriteLine($"FOUND in '{db}': {r.GetString(1)} | TenantKey={tk}");
            }
        }
        catch
        {
            /* skip */
        }
    }
}
