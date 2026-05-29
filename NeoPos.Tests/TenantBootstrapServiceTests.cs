using BusinessLayer.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NeoPos.Tests.TestHelpers;
using NeoPos.WebAPI.Services;

namespace NeoPos.Tests;

[Collection(SyncTestCollection.Name)]
public class TenantBootstrapServiceTests
{
    [Fact]
    public async Task BootstrapAsync_CreatesAdminWithBcrypt_AndSyncMetadata()
    {
        await using var db = await SyncTestDbFactory.CreateAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["NeoPos:Mode"] = "tenant",
                ["NeoPos:TenantKey"] = "bootstrap-tenant",
                ["NeoPos:AdminUsername"] = "localadmin",
                ["NeoPos:AdminPassword"] = "LocalPass99!",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<DAL.Server.Context.AppDbContext>(_ => db.LocalDb);
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddScoped<TenantBootstrapService>();

        var sp = services.BuildServiceProvider();
        await sp.GetRequiredService<TenantBootstrapService>().BootstrapAsync();

        var user = await db.LocalDb.Users.FirstAsync(u => u.Username == "localadmin");
        Assert.True(PasswordHashHelper.IsBcryptHash(user.PasswordHash));
        Assert.True(PasswordHashHelper.Verify("LocalPass99!", user.PasswordHash));

        var meta = await db.LocalDb.LocalSyncMetadata.FirstAsync();
        Assert.Equal("bootstrap-tenant", meta.TenantKey);
        Assert.NotNull(meta.LastSuccessfulSyncAt);
    }

    [Fact]
    public async Task BootstrapAsync_SecondRun_UpdatesPasswordWhenConfigChanges()
    {
        await using var db = await SyncTestDbFactory.CreateAsync();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["NeoPos:Mode"] = "tenant",
                ["NeoPos:TenantKey"] = "pw-change",
                ["NeoPos:AdminUsername"] = "admin",
                ["NeoPos:AdminPassword"] = "FirstPass1",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<DAL.Server.Context.AppDbContext>(_ => db.LocalDb);
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddScoped<TenantBootstrapService>();
        var sp = services.BuildServiceProvider();

        await sp.GetRequiredService<TenantBootstrapService>().BootstrapAsync();

        config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["NeoPos:Mode"] = "tenant",
                ["NeoPos:TenantKey"] = "pw-change",
                ["NeoPos:AdminUsername"] = "admin",
                ["NeoPos:AdminPassword"] = "SecondPass2",
            })
            .Build();

        var services2 = new ServiceCollection();
        services2.AddSingleton<DAL.Server.Context.AppDbContext>(_ => db.LocalDb);
        services2.AddSingleton<IConfiguration>(config);
        services2.AddLogging();
        services2.AddScoped<TenantBootstrapService>();
        var sp2 = services2.BuildServiceProvider();

        await sp2.GetRequiredService<TenantBootstrapService>().BootstrapAsync();

        var user = await db.LocalDb.Users.FirstAsync(u => u.Username == "admin");
        Assert.True(PasswordHashHelper.Verify("SecondPass2", user.PasswordHash));
    }
}
