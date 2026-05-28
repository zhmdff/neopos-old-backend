using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DAL.Server.Configurations;

public class HallTimeDiscountRuleConfiguration : IEntityTypeConfiguration<HallTimeDiscountRule>
{
    public void Configure(EntityTypeBuilder<HallTimeDiscountRule> builder)
    {
        builder.Property(r => r.DiscountPercentage).HasPrecision(5, 2);
        builder.Property(r => r.DiscountAmount).HasPrecision(18, 2);
        builder.Property(r => r.Label).HasMaxLength(120);

        builder.HasOne(r => r.Hall)
            .WithMany()
            .HasForeignKey(r => r.HallId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => new { r.CompanyId, r.HallId });
    }
}
