using Domain.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DAL.Server.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.NameAz).IsRequired().HasMaxLength(100);

        // EF Core 8: int[] is handled as a primitive collection.
        // Npgsql will use 'integer[]', SQLite will use JSON.
        builder.Property(r => r.Permissions);
    }
}