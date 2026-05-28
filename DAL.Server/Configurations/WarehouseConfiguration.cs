using Domain.Common.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace DAL.Server.Configurations;

public class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> builder)
    {
        builder.Property(w => w.Name).IsRequired().HasMaxLength(150);
        builder.Property(w => w.Address).HasMaxLength(500);

        // Bir şirkətin eyni adda iki anbarı olmasın
        builder.HasIndex(w => new { w.Name, w.CompanyId }).IsUnique();
    }
}
