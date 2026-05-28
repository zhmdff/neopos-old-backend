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

        // PostgreSQL massiv tipini EF Core-a tanıdırıq
        builder.Property(r => r.Permissions)
            .HasColumnType("integer[]");
    }
}