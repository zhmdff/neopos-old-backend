using DAL.Server.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DAL.Server;

public static class DALRegistration
{
    public static void RegisterDAL(this IServiceCollection service, IConfiguration configuration)
    {
        // Local database (SQLite) for primary operations
        service.AddDbContext<AppDbContext>(opt =>
        {
            opt.UseSqlite(configuration.GetConnectionString("Sqlite"));
        });

        // Remote database (PostgreSQL) for synchronization
        service.AddDbContext<RemoteDbContext>(opt =>
        {
            opt.UseNpgsql(configuration.GetConnectionString("RemotePostgres"));
        });
    }
}