using Domain.Entities;
using Domain.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DAL.Server.Configurations;

public class OrderDetailConfiguration : IEntityTypeConfiguration<OrderDetail>
{
    public void Configure(EntityTypeBuilder<OrderDetail> builder)
    {
        builder.Property(od => od.ProductName).IsRequired().HasMaxLength(200);
        builder.Property(od => od.ProductVariantName).HasMaxLength(200);
        builder.Property(od => od.ItemNote).HasMaxLength(200);
        builder.Property(od => od.KitchenCompositionNote).HasMaxLength(1000);

        builder.Property(od => od.Price).HasPrecision(18, 2);
        builder.Property(od => od.TotalPrice).HasPrecision(18, 2);

        builder.Property(od => od.Quantity).IsRequired();
        builder.Property(od => od.SplitGroup).HasDefaultValue(0);

        builder.HasOne(od => od.Product)
               .WithMany()
               .HasForeignKey(od => od.ProductId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ProductVariant>()
            .WithMany()
            .HasForeignKey(od => od.ProductVariantId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}