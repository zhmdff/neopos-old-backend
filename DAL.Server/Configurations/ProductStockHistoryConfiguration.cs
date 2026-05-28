using Domain.Common.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace DAL.Server.Configurations;

public class ProductStockHistoryConfiguration : IEntityTypeConfiguration<ProductStockHistory>
{
    public void Configure(EntityTypeBuilder<ProductStockHistory> builder)
    {
        // Çəki məsələsi üçün 4 rəqəm dəqiqlik (Məs: 2.5678 kq)
        builder.Property(h => h.QuantityBefore).HasPrecision(18, 4);
        builder.Property(h => h.ChangeAmount).HasPrecision(18, 4);
        builder.Property(h => h.QuantityAfter).HasPrecision(18, 4);

        builder.Property(h => h.Note).HasMaxLength(500);

        // İlişkilər
        builder.HasOne(h => h.Product)
               .WithMany()
               .HasForeignKey(h => h.ProductId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(h => h.Warehouse)
               .WithMany()
               .HasForeignKey(h => h.WarehouseId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(h => h.Supplier)
               .WithMany()
               .HasForeignKey(h => h.SupplierId)
               .OnDelete(DeleteBehavior.SetNull);
    }
}
