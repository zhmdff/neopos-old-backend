namespace Domain.Entities;

/// <summary>
/// Boss brauzerində Web Push (VAPID) abunəliyi — kritik auditlər (məhsul silinmə, arxiv çek və s.).
/// </summary>
public class BossWebPushSubscription
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid CompanyId { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string P256dh { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
