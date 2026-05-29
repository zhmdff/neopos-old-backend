using AutoMapper;
using BusinessLayer.DTOs.User;
using BusinessLayer.Profiles;
using BusinessLayer.Services.Implementations;
using BusinessLayer.Utilities;
using DAL.Server.Context;
using DAL.Server.Service;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using NeoPos.Tests.TestHelpers;

namespace NeoPos.Tests;

public class UserServicePasswordTests
{
    [Fact]
    public async Task CreateAsync_StoresBcryptHash_NotPlaintext()
    {
        var currentUser = new Mock<ICurrentUserService>();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        await using var ctx = new AppDbContext(options, currentUser.Object);

        var companyId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        ctx.Companies.Add(TestEntityFactory.CreateCompany(companyId, "t1"));
        ctx.Roles.Add(TestEntityFactory.CreateAdminRole(roleId, companyId));
        await ctx.SaveChangesAsync();

        var mapper = new MapperConfiguration(cfg => cfg.AddProfile<UserProfile>()).CreateMapper();
        var service = new UserService(ctx, mapper);

        await service.CreateAsync(new UserPostDto
        {
            CompanyId = companyId,
            RoleId = roleId,
            FullName = "Waiter",
            Username = "waiter1",
            Password = "WaiterPass99",
            PinCode = "2222",
        });

        var user = await ctx.Users.FirstAsync(u => u.Username == "waiter1");
        Assert.True(PasswordHashHelper.IsBcryptHash(user.PasswordHash));
        Assert.True(PasswordHashHelper.Verify("WaiterPass99", user.PasswordHash));
        Assert.NotEqual("WaiterPass99", user.PasswordHash);
        Assert.False(user.IsSynced);
    }
}
