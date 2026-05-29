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
            
            // Use MigrateAsync to ensure history table and migrations are applied
            await remoteDb.Database.MigrateAsync();
            
            logger.LogInformation("Remote PostgreSQL database is ready.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize remote PostgreSQL database. Ensure the server is reachable.");
        }
    }
}
