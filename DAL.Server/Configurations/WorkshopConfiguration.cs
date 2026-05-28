using Domain.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DAL.Server.Configurations;

public class WorkshopConfiguration : IEntityTypeConfiguration<Workshop>
{
    public void Configure(EntityTypeBuilder<Workshop> builder)
    {
        builder.Property(w => w.NameAz).IsRequired().HasMaxLength(100);
        builder.Property(w => w.NameEn).HasMaxLength(100);
        builder.Property(w => w.NameRu).HasMaxLength(100);

        builder.Property(w => w.IsPrinting).HasDefaultValue(true);

        builder.HasOne(w => w.Company)
               .WithMany()
               .HasForeignKey(w => w.CompanyId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}