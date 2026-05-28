using Domain.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DAL.Server.Configurations;

public class CashShiftExpenseConfiguration : IEntityTypeConfiguration<CashShiftExpense>
{
    public void Configure(EntityTypeBuilder<CashShiftExpense> builder)
    {
        builder.ToTable("CashShiftExpenses");

        builder.Property(x => x.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.Note)
            .HasMaxLength(1000)
            .IsRequired();

        builder.HasIndex(x => x.CompanyId);
        builder.HasIndex(x => x.CashShiftId);

        builder.HasOne(x => x.CashShift)
            .WithMany(s => s.Expenses)
            .HasForeignKey(x => x.CashShiftId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.RecordedByUser)
            .WithMany()
            .HasForeignKey(x => x.RecordedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
