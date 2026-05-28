using DAL.Server.Service;
using Microsoft.EntityFrameworkCore;

namespace DAL.Server.Context;

public class RemoteDbContext : AppDbContext
{
    public RemoteDbContext(DbContextOptions<RemoteDbContext> options, ICurrentUserService currentUserService) 
        : base(options, currentUserService)
    {
    }
}
