using Domain.Common.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace DAL.Server.Configurations;

public class QRMenuSettingConfiguration : IEntityTypeConfiguration<QRMenuSetting>
{
    public void Configure(EntityTypeBuilder<QRMenuSetting> builder)
    {
        // 1. ƏLAQƏ: Tək və dəqiq əlaqə. CompanyId1 yaranmasının qarşısını alan əsas hissə budur.
        builder.HasOne(x => x.Company)
            .WithOne()
            .HasForeignKey<QRMenuSetting>(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        // 2. RƏQƏM TƏNZİMLƏMƏSİ
        builder.Property(x => x.ServiceChargePercent)
            .HasPrecision(18, 2)
            .HasDefaultValue(0);

        // 3. QALEREYA ŞƏKİLLƏRİ (JSON)
        builder.Property(x => x.GalleryImages)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions)null) ?? new List<string>()
            )
            .Metadata.SetValueComparer(new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<List<string>>(
                (c1, c2) => c1.SequenceEqual(c2),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToList()));

        // 4. UZUNLUQ MƏHDUDİYYƏTLƏRİ
        builder.Property(x => x.WifiName).HasMaxLength(100);
        builder.Property(x => x.WifiPassword).HasMaxLength(100);
        builder.Property(x => x.WorkingHours).HasMaxLength(250);
        builder.Property(x => x.InstagramUrl).HasMaxLength(500);
        builder.Property(x => x.TiktokUrl).HasMaxLength(500);
        builder.Property(x => x.FacebookUrl).HasMaxLength(500);
        builder.Property(x => x.MapLocationUrl).HasMaxLength(2000);
    }
}