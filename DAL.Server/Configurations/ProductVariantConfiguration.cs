using Domain.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DAL.Server.Configurations;

public class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.Property(v => v.NameAz).IsRequired().HasMaxLength(150);
        builder.Property(v => v.NameEn).HasMaxLength(150);
        builder.Property(v => v.NameRu).HasMaxLength(150);

        builder.Property(v => v.Barcode).HasMaxLength(50);
        builder.Property(v => v.Price).HasPrecision(18, 2);
        builder.Property(v => v.DeliveryPrice).HasPrecision(18, 2);
        builder.Property(v => v.OrderIndex).HasDefaultValue(0);

        builder.HasOne(v => v.Product)
            .WithMany(p => p.Variants)
            .HasForeignKey(v => v.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable("ProductVariants");
    }
}

