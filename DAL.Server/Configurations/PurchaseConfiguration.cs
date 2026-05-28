using Domain.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DAL.Server.Configurations;

public class PurchaseConfiguration : IEntityTypeConfiguration<Purchase>
{
    public virtual void Configure(EntityTypeBuilder<Purchase> builder)
    {
        // Ana qaimə məlumatları
        builder.Property(p => p.TotalAmount).HasPrecision(18, 2);
        builder.Property(p => p.InvoiceNumber).HasMaxLength(50);
        builder.Property(p => p.PurchaseDate).IsRequired();

        // Supplier ilə əlaqə
        builder.HasOne(p => p.Supplier)
               .WithMany()
               .HasForeignKey(p => p.SupplierId)
               .OnDelete(DeleteBehavior.Restrict); // Tədarükçü silinəndə qaimələr silinməsin

        // Warehouse ilə əlaqə
        builder.HasOne(p => p.Warehouse)
               .WithMany()
               .HasForeignKey(p => p.WarehouseId)
               .OnDelete(DeleteBehavior.Restrict);

        // PurchaseItem ilə 1-to-Many əlaqəsi
        builder.HasMany(p => p.PurchaseItems)
               .WithOne(pi => pi.Purchase)
               .HasForeignKey(pi => pi.PurchaseId)
               .OnDelete(DeleteBehavior.Cascade); // Qaimə silinsə, içindəki mallar da silinsin
    }
}