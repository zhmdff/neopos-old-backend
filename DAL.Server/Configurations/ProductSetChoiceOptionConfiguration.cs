using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DAL.Server.Configurations;

public class ProductSetChoiceOptionConfiguration : IEntityTypeConfiguration<ProductSetChoiceOption>
{
    public void Configure(EntityTypeBuilder<ProductSetChoiceOption> builder)
    {
        builder.Property(o => o.Quantity).IsRequired();

        builder.HasOne(o => o.Product)
            .WithMany()
            .HasForeignKey(o => o.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
