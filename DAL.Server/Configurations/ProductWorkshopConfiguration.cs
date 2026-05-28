using Domain.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DAL.Server.Configurations;

public class ProductWorkshopConfiguration : IEntityTypeConfiguration<ProductWorkshop>
{
    public void Configure(EntityTypeBuilder<ProductWorkshop> builder)
    {
        builder.ToTable("ProductWorkshops");

        builder.HasIndex(x => new { x.CompanyId, x.ProductId, x.WorkshopId }).IsUnique();

        builder.HasOne(x => x.Product)
            .WithMany(p => p.AdditionalWorkshops)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Workshop)
            .WithMany()
            .HasForeignKey(x => x.WorkshopId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

