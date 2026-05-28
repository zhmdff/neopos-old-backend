using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DAL.Server.Configurations;

public class BossWebPushSubscriptionConfiguration : IEntityTypeConfiguration<BossWebPushSubscription>
{
    public void Configure(EntityTypeBuilder<BossWebPushSubscription> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Endpoint).IsRequired().HasMaxLength(2048);
        builder.Property(x => x.P256dh).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Auth).IsRequired().HasMaxLength(200);
        builder.HasIndex(x => x.Endpoint).IsUnique();
        builder.HasIndex(x => x.CompanyId);
    }
}
