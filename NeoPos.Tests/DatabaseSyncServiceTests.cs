using BusinessLayer.Utilities;
using Domain.Common.Entities;
using Microsoft.EntityFrameworkCore;
using NeoPos.Tests.TestHelpers;

namespace NeoPos.Tests;

[Collection(SyncTestCollection.Name)]
public class DatabaseSyncServiceTests
{
    [Fact]
    public async Task TriggerSync_PushesUnsyncedUser_ToRemote_WithMasterCompanyId()
    {
        await using var factory = await SyncTestDbFactory.CreateAsync();
        var localCompanyId = Guid.NewGuid();
        var masterCompanyId = Guid.NewGuid();
        const string tenantKey = "cafe-sync-test";

        factory.RemoteDb.Companies.Add(TestEntityFactory.CreateCompany(masterCompanyId, tenantKey));
        await factory.RemoteDb.SaveChangesAsync();

        var roleId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        factory.LocalDb.Companies.Add(TestEntityFactory.CreateCompany(localCompanyId, tenantKey));
        factory.LocalDb.Roles.Add(TestEntityFactory.CreateAdminRole(roleId, localCompanyId));
        factory.LocalDb.Users.Add(TestEntityFactory.CreateUser(
            userId,
            localCompanyId,
            roleId,
            "staff_user",
            PasswordHashHelper.Hash("pass123"),
            isSynced: false));
        factory.LocalDb.LocalSyncMetadata.Add(TestEntityFactory.CreateSyncMetadata(tenantKey));
        await factory.LocalDb.SaveChangesAsync();

        var sync = factory.CreateSyncService();
        await sync.TriggerSyncAsync();

        var remoteUser = await factory.RemoteDb.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);
        Assert.NotNull(remoteUser);
        Assert.Equal(masterCompanyId, remoteUser!.CompanyId);
        Assert.Equal("staff_user", remoteUser.Username);
        Assert.True(PasswordHashHelper.Verify("pass123", remoteUser.PasswordHash));

        var localUser = await factory.LocalDb.Users.AsNoTracking().FirstAsync(u => u.Id == userId);
        Assert.True(localUser.IsSynced);
    }

    [Fact]
    public async Task TriggerSync_PushesUnsyncedRole_BeforeUserCanReferenceIt()
    {
        await using var factory = await SyncTestDbFactory.CreateAsync();
        var companyId = Guid.NewGuid();
        const string tenantKey = "role-sync";

        factory.RemoteDb.Companies.Add(TestEntityFactory.CreateCompany(companyId, tenantKey));
        factory.LocalDb.Companies.Add(TestEntityFactory.CreateCompany(companyId, tenantKey));
        await factory.LocalDb.SaveChangesAsync();
        await factory.RemoteDb.SaveChangesAsync();

        var roleId = Guid.NewGuid();
        var role = TestEntityFactory.CreateAdminRole(roleId, companyId);
        role.IsSynced = false;
        factory.LocalDb.Roles.Add(role);
        factory.LocalDb.LocalSyncMetadata.Add(TestEntityFactory.CreateSyncMetadata(tenantKey));
        await factory.LocalDb.SaveChangesAsync();

        await factory.CreateSyncService().TriggerSyncAsync();

        Assert.True(await factory.RemoteDb.Roles.AnyAsync(r => r.Id == roleId));
    }

    [Fact]
    public async Task TriggerSync_PullsBossProduct_IntoLocal_WhenModifiedOnRemote()
    {
        await using var factory = await SyncTestDbFactory.CreateAsync();
        var companyId = Guid.NewGuid();
        const string tenantKey = "product-pull";

        factory.LocalDb.Companies.Add(TestEntityFactory.CreateCompany(companyId, tenantKey));
        factory.RemoteDb.Companies.Add(TestEntityFactory.CreateCompany(companyId, tenantKey));
        var meta = TestEntityFactory.CreateSyncMetadata(tenantKey);
        meta.LastSuccessfulSyncAt = DateTime.UtcNow.AddHours(-2);
        factory.LocalDb.LocalSyncMetadata.Add(meta);

        var workshopId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        factory.RemoteDb.Workshops.Add(new Workshop
        {
            Id = workshopId,
            CompanyId = companyId,
            NameAz = "W",
            NameEn = "W",
            NameRu = "W",
            PrinterType = "Net",
            PrinterValue = "x",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Test",
            IsSynced = true,
        });
        factory.RemoteDb.Categories.Add(new Category
        {
            Id = categoryId,
            CompanyId = companyId,
            NameAz = "Cat",
            NameEn = "Cat",
            NameRu = "Cat",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Test",
            IsSynced = true,
        });
        factory.RemoteDb.Products.Add(new Product
        {
            Id = productId,
            CompanyId = companyId,
            CategoryId = categoryId,
            WorkshopId = workshopId,
            NameAz = "New Pizza",
            NameEn = "New Pizza",
            NameRu = "New Pizza",
            SalePrice = 12m,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "Boss",
            LastModifiedAt = DateTime.UtcNow,
            IsSynced = true,
        });

        await factory.LocalDb.SaveChangesAsync();
        await factory.RemoteDb.SaveChangesAsync();

        await factory.CreateSyncService().TriggerSyncAsync();

        var localProduct = await factory.LocalDb.Products.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == productId);
        Assert.NotNull(localProduct);
        Assert.Equal("New Pizza", localProduct!.NameAz);
    }

    [Fact]
    public async Task TriggerSync_WithoutBootstrapMetadata_DoesNotThrow()
    {
        await using var factory = await SyncTestDbFactory.CreateAsync();
        factory.LocalDb.Companies.Add(TestEntityFactory.CreateCompany(Guid.NewGuid(), "no-meta"));
        await factory.LocalDb.SaveChangesAsync();

        await factory.CreateSyncService().TriggerSyncAsync();

        Assert.False(await factory.RemoteDb.Users.AnyAsync());
    }
}
