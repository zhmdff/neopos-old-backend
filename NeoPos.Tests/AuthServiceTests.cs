using AutoMapper;
using BusinessLayer.DTOs.Auth;
using BusinessLayer.ExternalServices.Abstractions;
using BusinessLayer.Profiles;
using BusinessLayer.Services.Implementations;
using BusinessLayer.Utilities;
using DAL.Server.Context;
using DAL.Server.Service;
using Domain.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using NeoPos.Tests.TestHelpers;

namespace NeoPos.Tests;

public class AuthServiceTests
{
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IJwtTokenService> _jwt = new();

    private AppDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, _currentUser.Object);
    }

    private static IMapper CreateMapper()
    {
        var config = new MapperConfiguration(cfg => cfg.AddProfile<AuthProfile>());
        return config.CreateMapper();
    }

    private static async Task SeedAdminUserAsync(
        AppDbContext ctx,
        string username,
        string passwordHash,
        bool isAdmin = true,
        DateTime? packageEnd = null)
    {
        var companyId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var company = TestEntityFactory.CreateCompany(companyId, "tenant-auth");
        if (packageEnd.HasValue)
            company.PackageEndDate = packageEnd.Value;
        ctx.Companies.Add(company);

        ctx.Roles.Add(TestEntityFactory.CreateAdminRole(roleId, companyId));
        ctx.Users.Add(TestEntityFactory.CreateUser(userId, companyId, roleId, username, passwordHash, isSynced: true));
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task LoginAsync_BcryptPassword_ReturnsToken()
    {
        await using var ctx = CreateContext(Guid.NewGuid().ToString());
        var hash = PasswordHashHelper.Hash("Admin123");
        await SeedAdminUserAsync(ctx, "superadmin", hash);

        _jwt.Setup(j => j.GenerateJwtToken(It.IsAny<User>())).ReturnsAsync("jwt-token");

        var service = new AuthService(ctx, CreateMapper(), _jwt.Object);
        var result = await service.LoginAsync(new LoginRequestDTO { Username = "superadmin", Password = "Admin123" });

        Assert.Equal("jwt-token", result.Token);
        Assert.True(result.RoleIsAdmin);
    }

    [Fact]
    public async Task LoginAsync_Username_IsCaseInsensitive()
    {
        await using var ctx = CreateContext(Guid.NewGuid().ToString());
        var hash = PasswordHashHelper.Hash("pass");
        await SeedAdminUserAsync(ctx, "MyUser", hash);

        _jwt.Setup(j => j.GenerateJwtToken(It.IsAny<User>())).ReturnsAsync("t");

        var service = new AuthService(ctx, CreateMapper(), _jwt.Object);
        var result = await service.LoginAsync(new LoginRequestDTO { Username = "myuser", Password = "pass" });
        Assert.Equal("t", result.Token);
    }

    [Fact]
    public async Task LoginAsync_LegacyPlaintext_UpgradesToBcrypt()
    {
        await using var ctx = CreateContext(Guid.NewGuid().ToString());
        await SeedAdminUserAsync(ctx, "legacy", "plainPass");

        _jwt.Setup(j => j.GenerateJwtToken(It.IsAny<User>())).ReturnsAsync("t");

        var service = new AuthService(ctx, CreateMapper(), _jwt.Object);
        await service.LoginAsync(new LoginRequestDTO { Username = "legacy", Password = "plainPass" });

        var user = await ctx.Users.FirstAsync(u => u.Username == "legacy");
        Assert.True(PasswordHashHelper.IsBcryptHash(user.PasswordHash));
        Assert.True(PasswordHashHelper.Verify("plainPass", user.PasswordHash));
        Assert.False(user.IsSynced);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_Throws()
    {
        await using var ctx = CreateContext(Guid.NewGuid().ToString());
        await SeedAdminUserAsync(ctx, "u", PasswordHashHelper.Hash("right"));

        var service = new AuthService(ctx, CreateMapper(), _jwt.Object);
        await Assert.ThrowsAsync<Exception>(() =>
            service.LoginAsync(new LoginRequestDTO { Username = "u", Password = "wrong" }));
    }

    [Fact]
    public async Task LoginAsync_ExpiredPackage_Throws()
    {
        await using var ctx = CreateContext(Guid.NewGuid().ToString());
        await SeedAdminUserAsync(ctx, "u", PasswordHashHelper.Hash("p"), packageEnd: DateTime.UtcNow.AddDays(-10));

        var service = new AuthService(ctx, CreateMapper(), _jwt.Object);
        var ex = await Assert.ThrowsAsync<Exception>(() =>
            service.LoginAsync(new LoginRequestDTO { Username = "u", Password = "p" }));
        Assert.Contains("Lisenziya", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
