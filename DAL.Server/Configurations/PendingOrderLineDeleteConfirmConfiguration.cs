using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DAL.Server.Configurations;

public class PendingOrderLineDeleteConfirmConfiguration : IEntityTypeConfiguration<PendingOrderLineDeleteConfirm>
{
    public void Configure(EntityTypeBuilder<PendingOrderLineDeleteConfirm> builder)
    {
        builder.ToTable("PendingOrderLineDeleteConfirms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PendingId).HasMaxLength(40).IsRequired();
        builder.HasIndex(x => x.PendingId).IsUnique();
        builder.HasIndex(x => new { x.CompanyId, x.Status });
        builder.Property(x => x.TableName).HasMaxLength(200);
        builder.Property(x => x.ProductName).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ReasonSnapshot).HasMaxLength(2000);
        builder.Property(x => x.RequestedByDisplayName).HasMaxLength(200);
        builder.Property(x => x.TelegramConfirmMessageRefsJson).HasMaxLength(8000);
    }
}
