using Microsoft.Extensions.Configuration;
using NeoPos.Migration;

static void PrintHelp()
{
    Console.WriteLine("""
        NeoPos legacy → renewed migration (company + roles + users)

        Usage:
          dotnet run --project NeoPos.Migration -- [options]

        Options (override appsettings.migration.json):
          --source <connectionString>     Legacy PostgreSQL (old system)
          --source-schema <name>          Default: public
          --target <postgres|sqlite>      Destination database
          --target-connection <cs>        Target connection string
          --company-id <guid>             Legacy company UUID to migrate
          --tenant-key <string>           TenantKey for renewed system (required)
          --dry-run                       Preview only
          --update-tenant-key             Set TenantKey on existing company row
          --help

        Examples:
          # Dry-run against Neon master
          dotnet run --project NeoPos.Migration -- --dry-run

          # Migrate users to local SQLite tenant DB
          dotnet run --project NeoPos.Migration -- --target sqlite --target-connection "Data Source=neopos_local.db"

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

if (string.IsNullOrWhiteSpace(sourceCs) || string.IsNullOrWhiteSpace(targetCs) ||
    string.IsNullOrWhiteSpace(companyIdStr) || string.IsNullOrWhiteSpace(tenantKey))
{
    Console.Error.WriteLine("Missing required settings. Provide appsettings.migration.json or CLI args. Use --help.");
    return 1;
}

if (!Guid.TryParse(companyIdStr, out var companyId))
{
    Console.Error.WriteLine($"Invalid --company-id: {companyIdStr}");
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
        if (key is "dry-run" or "update-tenant-key" or "help")
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
