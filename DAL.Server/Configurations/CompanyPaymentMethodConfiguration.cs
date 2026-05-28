using Domain.Common.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DAL.Server.Configurations;

public class CompanyPaymentMethodConfiguration : IEntityTypeConfiguration<CompanyPaymentMethod>
{
    public void Configure(EntityTypeBuilder<CompanyPaymentMethod> builder)
    {
        builder.Property(x => x.NameAz).IsRequired().HasMaxLength(120);
        builder.HasIndex(x => new { x.CompanyId, x.SortOrder });
        builder.HasOne(x => x.Company)
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
