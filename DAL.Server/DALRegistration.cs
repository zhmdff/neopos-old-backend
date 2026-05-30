using DAL.Server.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DAL.Server;

public static class DALRegistration
{
    public static void RegisterDAL(this IServiceCollection service, IConfiguration configuration)
    {
        // Detect if master or tenant
        string mode = Environment.GetEnvironmentVariable("NEOPOS_MODE") 
                      ?? configuration["NeoPos:Mode"] 
                      ?? "tenant"; // Default is tenant

        bool usePostgres = mode.Equals("master", StringComparison.OrdinalIgnoreCase);

        string? remotePostgres = configuration.GetConnectionString("RemotePostgres");
        if (string.IsNullOrEmpty(remotePostgres) || remotePostgres.Contains("localhost") || remotePostgres.Contains("127.0.0.1"))
        {
            remotePostgres = configuration.GetConnectionString("RemotePostgresLocal")
                               ?? "Host=localhost;Port=5432;Database=neopos_sync;Username=postgres;Password=zhmdff123";
        }

        // Primary database: master uses PostgreSQL, tenant uses SQLite
        service.AddDbContext<AppDbContext>(opt =>
        {
            if (usePostgres)
                opt.UseNpgsql(remotePostgres);
            else
                opt.UseSqlite(configuration.GetConnectionString("Sqlite"));
        });

        // Remote PostgreSQL context: needed by master for migrations, 
        // and needed by tenant for DatabaseSyncService to push/pull data.
        service.AddDbContext<RemoteDbContext>(opt =>
        {
            opt.UseNpgsql(remotePostgres);
        });
    }
}