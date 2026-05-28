using Domain.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DAL.Server.Configurations;

public class PurchaseItemConfiguration : IEntityTypeConfiguration<PurchaseItem>
{
    public void Configure(EntityTypeBuilder<PurchaseItem> builder)
    {
        // Rəqəmlərin dəqiqliyi
        builder.Property(pi => pi.Quantity).HasPrecision(18, 3); // Məsələn: 2.550 kq üçün
        builder.Property(pi => pi.PriceAtPurchase).HasPrecision(18, 2);

        // Product ilə əlaqə
        builder.HasOne(pi => pi.Product)
               .WithMany()
               .HasForeignKey(pi => pi.ProductId)
               .OnDelete(DeleteBehavior.Restrict); // Məhsul silinə bilməsin əgər alış sənədində varsa
    }
}