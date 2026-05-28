using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DAL.Server.Configurations;

public class OrderHeaderConfiguration : IEntityTypeConfiguration<OrderHeader>
{
    public void Configure(EntityTypeBuilder<OrderHeader> builder)
    {
        // --- QƏBZ NÖMRƏSİ ---
        builder.Property(oh => oh.CheckNumber).IsRequired().HasMaxLength(50);



        // --- MƏTN SAHƏLƏRİ ---
        builder.Property(oh => oh.Note).HasMaxLength(500);
        builder.Property(oh => oh.WaiterName).HasMaxLength(100);
        builder.Property(oh => oh.CashierName).HasMaxLength(100);

        // --- ƏLAQƏLƏR ---
        builder.HasOne(oh => oh.Table)
               .WithMany() // Bir masanın çoxlu çek tarixçəsi ola bilər
               .HasForeignKey(oh => oh.TableId)
               .OnDelete(DeleteBehavior.Restrict);

        // Bir Çekin çoxlu məhsulu (Details) var
        builder.HasMany(oh => oh.OrderDetails)
               .WithOne(od => od.OrderHeader)
               .HasForeignKey(od => od.OrderHeaderId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(oh => oh.Customer)
            .WithMany()
            .HasForeignKey(oh => oh.CustomerId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(oh => oh.CashShift)
            .WithMany()
            .HasForeignKey(oh => oh.CashShiftId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(oh => oh.CustomPaymentMethod)
            .WithMany()
            .HasForeignKey(oh => oh.CustomPaymentMethodId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}