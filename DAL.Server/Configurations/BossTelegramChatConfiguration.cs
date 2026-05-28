using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DAL.Server.Configurations;

public class BossTelegramChatConfiguration : IEntityTypeConfiguration<BossTelegramChat>
{
    public void Configure(EntityTypeBuilder<BossTelegramChat> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.CompanyId);
        builder.HasIndex(x => x.ChatId);
        builder.HasIndex(x => new { x.CompanyId, x.ChatId }).IsUnique();
    }
}
