using Domain.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DAL.Server.Configurations;

public class HallConfiguration : IEntityTypeConfiguration<Hall>
{
    public void Configure(EntityTypeBuilder<Hall> builder)
    {
        builder.Property(h => h.NameAz).IsRequired().HasMaxLength(100);
        builder.Property(h => h.NameEn).HasMaxLength(100);
        builder.Property(h => h.NameRu).HasMaxLength(100);

        builder.Property(h => h.ServicePercentage).HasPrecision(5, 2);
        builder.Property(h => h.IsGuestCountEnabled)
            .HasDefaultValue(true);
        builder.Property(h => h.IsTableHourActive)
            .HasDefaultValue(false);

        builder.HasMany(h => h.Tables)
               .WithOne(t => t.Hall)
               .HasForeignKey(t => t.HallId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(h => h.Company)
               .WithMany()
               .HasForeignKey(h => h.CompanyId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}