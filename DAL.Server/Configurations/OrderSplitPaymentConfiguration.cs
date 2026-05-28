using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DAL.Server.Configurations;

public class OrderSplitPaymentConfiguration : IEntityTypeConfiguration<OrderSplitPayment>
{
    public void Configure(EntityTypeBuilder<OrderSplitPayment> builder)
    {
        builder.Property(x => x.PaidCash).HasPrecision(18, 2);
        builder.Property(x => x.PaidCard).HasPrecision(18, 2);

        builder.HasOne(x => x.OrderHeader)
            .WithMany(h => h.SplitPayments)
            .HasForeignKey(x => x.OrderHeaderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
