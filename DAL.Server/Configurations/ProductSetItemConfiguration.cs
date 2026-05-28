using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DAL.Server.Configurations;

public class ProductSetItemConfiguration : IEntityTypeConfiguration<ProductSetItem>
{
    public void Configure(EntityTypeBuilder<ProductSetItem> builder)
    {
        builder.Property(psi => psi.Quantity).IsRequired();

        builder.HasOne(psi => psi.Product)
               .WithMany()
               .HasForeignKey(psi => psi.ProductId)
               .OnDelete(DeleteBehavior.Restrict); 
    }
}