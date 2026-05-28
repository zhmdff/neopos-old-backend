using Domain.Common.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DAL.Server.Configurations;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.TablesLayoutMode)
            .HasConversion<int>()
            .HasDefaultValue(TablesLayoutMode.Normal);
        builder.Property(c => c.NameAz).IsRequired().HasMaxLength(200);
        builder.Property(c => c.NameRu).HasMaxLength(200);
        builder.Property(c => c.NameEn).HasMaxLength(200);
        builder.Property(c => c.PhoneNumber1).IsRequired().HasMaxLength(20);
        builder.Property(c => c.KassaReceiptThankYouText).HasMaxLength(500);
        builder.Property(c => c.PosLockScreenImage).HasMaxLength(500);
        builder.Property(c => c.CustomerDisplayLockScreenImage).HasMaxLength(500);
        builder.Property(c => c.TelegramBotToken).HasMaxLength(512);
        builder.Property(c => c.TelegramNotifyPrefsJson).HasMaxLength(4000);
    }
}
