using Domain.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DAL.Server.Configurations;

public class TableConfiguration : IEntityTypeConfiguration<Table>
{
    public void Configure(EntityTypeBuilder<Table> builder)
    {
        builder.Property(t => t.NameAz).IsRequired().HasMaxLength(50);
        builder.Property(t => t.NameEn).HasMaxLength(50);
        builder.Property(t => t.NameRu).HasMaxLength(50);

        builder.Property(t => t.DepositAmount).HasPrecision(18, 2);
        builder.Property(t => t.TableHourLimitMinutes);
        builder.Property(t => t.MapPositionX).HasPrecision(6, 2);
        builder.Property(t => t.MapPositionY).HasPrecision(6, 2);
        builder.Property(t => t.MapWidthPercent).HasPrecision(6, 2);
        builder.Property(t => t.MapHeightPercent).HasPrecision(6, 2);
        builder.Property(t => t.MapShape)
            .HasConversion<int>()
            .HasDefaultValue(Domain.Enums.TableMapShape.Rectangle);

        builder.Property(t => t.Status)
            .HasDefaultValue(Domain.Enums.TableStatus.Empty)
            .HasSentinel((Domain.Enums.TableStatus)0);

        // Şirkət əlaqəsi
        builder.HasOne(t => t.Company)
               .WithMany()
               .HasForeignKey(t => t.CompanyId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}