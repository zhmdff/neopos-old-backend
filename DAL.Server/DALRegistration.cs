using DAL.Server.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DAL.Server;

public static class DALRegistration
{
    public static void RegisterDAL(this IServiceCollection service, IConfiguration configuration)
    {
        bool usePostgres = configuration["NeoPos:UsePostgresAsPrimary"]?.ToLower() == "true";

        // Local database for primary operations
        service.AddDbContext<AppDbContext>(opt =>
        {
            if (usePostgres)
            {
                opt.UseNpgsql(configuration.GetConnectionString("RemotePostgres"));
            }
            else
            {
                opt.UseSqlite(configuration.GetConnectionString("Sqlite"));
            }
        });

        // Remote database (PostgreSQL) for synchronization
        service.AddDbContext<RemoteDbContext>(opt =>
        {
            opt.UseNpgsql(configuration.GetConnectionString("RemotePostgres"));
        });
    }
}