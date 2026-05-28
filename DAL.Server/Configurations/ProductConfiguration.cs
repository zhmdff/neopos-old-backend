using Domain.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DAL.Server.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.Property(p => p.NameAz).IsRequired().HasMaxLength(150);
        builder.Property(p => p.NameEn).HasMaxLength(150);
        builder.Property(p => p.NameRu).HasMaxLength(150);

        builder.Property(p => p.Barcode).HasMaxLength(50);
        builder.Property(p => p.OrderIndex).HasDefaultValue(0);
        builder.Property(p => p.CostPrice).HasPrecision(18, 2);
        builder.Property(p => p.MarkupValue).HasPrecision(18, 2);
        builder.Property(p => p.SalePrice).HasPrecision(18, 2);
        builder.Property(p => p.Stock).HasDefaultValue(0).HasPrecision(18, 4);

        builder.Property(p => p.ShowInQr).HasDefaultValue(true);
        builder.Property(p => p.ShowInTerminal).HasDefaultValue(true);

        builder.Property(p => p.Unit).IsRequired();
        builder.Property(p => p.MarkupType).IsRequired();

        builder.HasOne(p => p.Category)
            .WithMany(p => p.Products)
            .HasForeignKey(p => p.CategoryId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull); // Kateqoriya silinəndə məhsul qalır (kateqoriyasız)

        builder.HasOne(p => p.Workshop)
            .WithMany()
            .HasForeignKey(p => p.WorkshopId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable("Products");
    }
}