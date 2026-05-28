using Domain.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DAL.Server.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.Property(c => c.NameAz).IsRequired().HasMaxLength(100);
        builder.Property(c => c.NameEn).HasMaxLength(100);
        builder.Property(c => c.NameRu).HasMaxLength(100);

        builder.Property(c => c.OrderIndex).HasDefaultValue(0);

        builder.HasOne(c => c.ParentCategory)
            .WithMany(c => c.SubCategories)
            .HasForeignKey(c => c.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict); // Ana silinəndə altlar qalsın və ya xəta versin

        builder.ToTable("Categories");
    }
}