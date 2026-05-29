using Domain.Common.Entities;
using Domain.Entities;

namespace NeoPos.Tests.TestHelpers;

public static class TestEntityFactory
{
    public static Company CreateCompany(Guid id, string tenantKey, Guid? alternateId = null)
    {
        var companyId = alternateId ?? id;
        return new Company
        {
            Id = companyId,
            TenantKey = tenantKey,
            NameAz = "Test Restoran",
            NameEn = "Test Restaurant",
            NameRu = "Test Restaurant",
            AddressAz = "Baku",
            AddressEn = "Baku",
            AddressRu = "Baku",
            PhoneNumber1 = "+994500000000",
            Slug = tenantKey.ToLowerInvariant(),
            PackageEndDate = DateTime.UtcNow.AddYears(2),
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            CreatedBy = "Test",
            IsSynced = false,
        };
    }

    public static Role CreateAdminRole(Guid id, Guid companyId) => new()
    {
        Id = id,
        CompanyId = companyId,
        NameAz = "Admin",
        NameEn = "Admin",
        NameRu = "Admin",
        IsAdmin = true,
        Permissions = Array.Empty<int>(),
        IsDeleted = false,
        CreatedAt = DateTime.UtcNow.AddDays(-30),
        CreatedBy = "Test",
        IsSynced = false,
    };

    public static User CreateUser(
        Guid id,
        Guid companyId,
        Guid roleId,
        string username,
        string passwordHash,
        bool isSynced = false) => new()
    {
        Id = id,
        CompanyId = companyId,
        RoleId = roleId,
        FullName = "Test User",
        Username = username,
        PasswordHash = passwordHash,
        PinCode = "1111",
        IsActive = true,
        IsDeleted = false,
        CreatedAt = DateTime.UtcNow.AddDays(-10),
        CreatedBy = "Test",
        IsSynced = isSynced,
    };

    public static LocalSyncMetadata CreateSyncMetadata(string tenantKey) => new()
    {
        Id = Guid.NewGuid(),
        TenantKey = tenantKey,
        LastSuccessfulSyncAt = DateTime.UtcNow.AddHours(-1),
        LastSyncStatus = "Test",
        IsSynced = false,
    };
}
