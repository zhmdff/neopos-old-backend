using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DAL.Server.Configurations;

public class ProductSetChoiceGroupConfiguration : IEntityTypeConfiguration<ProductSetChoiceGroup>
{
    public void Configure(EntityTypeBuilder<ProductSetChoiceGroup> builder)
    {
        builder.Property(g => g.NameAz).HasMaxLength(200).IsRequired();

        builder.HasOne(g => g.ProductSet)
            .WithMany(ps => ps.ChoiceGroups)
            .HasForeignKey(g => g.ProductSetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(g => g.Options)
            .WithOne(o => o.ChoiceGroup)
            .HasForeignKey(o => o.ProductSetChoiceGroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
