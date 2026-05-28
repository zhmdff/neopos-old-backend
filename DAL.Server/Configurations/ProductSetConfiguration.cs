using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DAL.Server.Configurations;

public class ProductSetConfiguration : IEntityTypeConfiguration<ProductSet>
{
    public void Configure(EntityTypeBuilder<ProductSet> builder)
    {
        builder.Property(ps => ps.Description).HasMaxLength(500);

        builder.HasOne(ps => ps.Product)
               .WithOne()
               .HasForeignKey<ProductSet>(ps => ps.ProductId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(ps => ps.SetItems)
               .WithOne(psi => psi.ProductSet)
               .HasForeignKey(psi => psi.ProductSetId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}