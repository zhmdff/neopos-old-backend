using Domain.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DAL.Server.Configurations;

public class CashShiftConfiguration : IEntityTypeConfiguration<CashShift>
{
    public void Configure(EntityTypeBuilder<CashShift> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.StartTime)
            .IsRequired();

        builder.Property(x => x.IsClosed)
            .HasDefaultValue(false);

        builder.Property(x => x.OpeningDepositAmount)
            .HasPrecision(18, 2)
            .HasDefaultValue(0m);

        builder.HasOne(x => x.OpenedByUser)
            .WithMany()
            .HasForeignKey(x => x.OpenedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ClosedByUser)
            .WithMany()
            .HasForeignKey(x => x.ClosedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.CompanyId);


        builder.HasIndex(x => new { x.CompanyId, x.IsClosed })
            .HasFilter("\"IsClosed\" = false")
            .IsUnique();
    }
}