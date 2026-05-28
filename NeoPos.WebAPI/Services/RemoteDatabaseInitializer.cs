using DAL.Server.Context;
using Microsoft.EntityFrameworkCore;

namespace NeoPos.WebAPI.Services;

/// <summary>
/// Ensures the remote PostgreSQL database is initialized with the correct schema
/// and all necessary tables exist.
/// </summary>
public static class RemoteDatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var remoteDb = scope.ServiceProvider.GetRequiredService<RemoteDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        try
        {
            logger.LogInformation("Checking remote PostgreSQL database initialization...");
            
            // This will create the database and all tables if they don't exist
            // on the remote PostgreSQL server defined in connection strings.
            await remoteDb.Database.EnsureCreatedAsync();
            
            logger.LogInformation("Remote PostgreSQL database is ready.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize remote PostgreSQL database. Ensure the server is reachable.");
        }
    }
}
