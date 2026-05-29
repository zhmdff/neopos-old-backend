using DAL.Server.Context;
using DAL.Server.Service;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NeoPos.WebAPI.Services;

namespace NeoPos.Tests.TestHelpers;

/// <summary>Dual SQLite in-memory databases for local (tenant) + remote (master) sync tests.</summary>
public sealed class SyncTestDbFactory : IAsyncDisposable
{
    private readonly SqliteConnection _localConnection;
    private readonly SqliteConnection _remoteConnection;

    public AppDbContext LocalDb { get; }
    public RemoteDbContext RemoteDb { get; }
    public IServiceProvider ServiceProvider { get; }

    private SyncTestDbFactory(
        SqliteConnection localConnection,
        SqliteConnection remoteConnection,
        AppDbContext localDb,
        RemoteDbContext remoteDb,
        IServiceProvider serviceProvider)
    {
        _localConnection = localConnection;
        _remoteConnection = remoteConnection;
        LocalDb = localDb;
        RemoteDb = remoteDb;
        ServiceProvider = serviceProvider;
    }

    public static async Task<SyncTestDbFactory> CreateAsync()
    {
        var dbId = Guid.NewGuid().ToString("N");
        var localConnection = new SqliteConnection($"Data Source=local_{dbId};Mode=Memory;Cache=Shared");
        var remoteConnection = new SqliteConnection($"Data Source=remote_{dbId};Mode=Memory;Cache=Shared");
        await localConnection.OpenAsync();
        await remoteConnection.OpenAsync();

        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(x => x.CompanyId).Returns((Guid?)null);

        var localOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(localConnection)
            .Options;
        var remoteOptions = new DbContextOptionsBuilder<RemoteDbContext>()
            .UseSqlite(remoteConnection)
            .Options;

        var localDb = new AppDbContext(localOptions, currentUser.Object);
        var remoteDb = new RemoteDbContext(remoteOptions, currentUser.Object);
        await localDb.Database.EnsureCreatedAsync();
        await remoteDb.Database.EnsureCreatedAsync();

        var services = new ServiceCollection();
        // Singleton so scoped sync/bootstrap scopes do not dispose shared test contexts.
        services.AddSingleton<AppDbContext>(_ => localDb);
        services.AddSingleton<RemoteDbContext>(_ => remoteDb);
        services.AddSingleton<IMediaSyncService, NoOpMediaSyncService>();
        services.AddLogging();

        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sync:IntervalMinutes"] = "5",
                ["Sync:InitialDelaySeconds"] = "0",
            })
            .Build();
        services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(configuration);
        services.AddSingleton<DatabaseSyncService>();

        return new SyncTestDbFactory(
            localConnection,
            remoteConnection,
            localDb,
            remoteDb,
            services.BuildServiceProvider());
    }

    public DatabaseSyncService CreateSyncService()
        => ServiceProvider.GetRequiredService<DatabaseSyncService>();

    public async ValueTask DisposeAsync()
    {
        await LocalDb.DisposeAsync();
        await RemoteDb.DisposeAsync();
        await _localConnection.DisposeAsync();
        await _remoteConnection.DisposeAsync();
        if (ServiceProvider is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else if (ServiceProvider is IDisposable disposable)
            disposable.Dispose();
    }
}

file sealed class NoOpMediaSyncService : IMediaSyncService
{
    public Task SyncUploadsAsync(MediaSyncRequest request, CancellationToken ct = default)
        => Task.CompletedTask;
}
