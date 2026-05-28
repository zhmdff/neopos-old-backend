using Domain.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace DAL.Server.Configurations;

public class KitchenOperationConfiguration : IEntityTypeConfiguration<KitchenOperation>
{
    public void Configure(EntityTypeBuilder<KitchenOperation> builder)
    {
        // Cədvəl adı
        builder.ToTable("KitchenOperations");

        // Primary Key
        builder.HasKey(x => x.Id);

        // Miqdarın dəqiqliyi
        builder.Property(x => x.Quantity)
            .IsRequired()
            .HasPrecision(18, 2);

        // Enum-un bazada string yox, int kimi saxlanılması (daha performanslıdır)
        builder.Property(x => x.OperationType)
            .IsRequired();

        // Məhsul adı (OrderDetail silinərsə audit üçün lazımdır)
        builder.Property(x => x.ProductName)
            .HasMaxLength(200)
            .IsRequired();

        // --- ƏLAQƏLƏR (Relationships) ---

        // OrderDetail ilə əlaqə
        builder.HasOne(x => x.OrderDetail)
            .WithMany() // Bir OrderDetail-in çoxlu çap əməliyyatı ola bilər
            .HasForeignKey(x => x.OrderDetailId)
            .OnDelete(DeleteBehavior.Cascade); // Məhsul tam ləğv edilərsə (bazadan silinərsə)

        // OrderHeader ilə əlaqə (Audit və hesabat üçün birbaşa bağlılıq)
        builder.HasIndex(x => x.OrderHeaderId);

        // Şirkət ID-si üzrə indeks (Filtrləmə üçün)
        builder.HasIndex(x => x.CompanyId);
    }
}
