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
            if (usePostgres)
            {
                remotePostgres = "Host=localhost;Port=5432;Database=neopos_new_db;Username=postgres;Password=Slome2006";
            }
            else
            {
                remotePostgres = "Host=37.60.247.244;Port=5432;Database=neopos_new_db;Username=postgres;Password=Slome2006";
            }
        }

        // Local database for primary operations
        service.AddDbContext<AppDbContext>(opt =>
        {
            if (usePostgres)
            {
                opt.UseNpgsql(remotePostgres);
            }
            else
            {
                opt.UseSqlite(configuration.GetConnectionString("Sqlite"));
            }
        });

        // Remote database (PostgreSQL) for synchronization
        service.AddDbContext<RemoteDbContext>(opt =>
        {
            opt.UseNpgsql(remotePostgres);
        });
    }
}