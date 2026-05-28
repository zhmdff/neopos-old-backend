namespace Domain.Entities;

/// <summary>Şirkət admin/kassir Telegram chat-i — kritik audit bildirişləri üçün.</summary>
public class BossTelegramChat
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid CompanyId { get; set; }
    public long ChatId { get; set; }
    public DateTime LinkedAt { get; set; }
}
