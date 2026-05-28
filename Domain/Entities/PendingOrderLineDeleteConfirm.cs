using Domain.Common;

namespace Domain.Entities;

/// <summary>
/// Terminal mətbəxə göndərilmiş sətir silinəndə Telegram + Boss panel təsdiqi üçün gözləmə qeydi.
/// Status: 0=Pending, 1=Accepted, 2=Rejected. İlk uğurlu resolve qalibdir.
/// </summary>
public class PendingOrderLineDeleteConfirm : BaseEntity
{
    public Guid CompanyId { get; set; }

    /// <summary>Terminal/Electron ilə eyni (məs. S482901).</summary>
    public string PendingId { get; set; } = string.Empty;

    public Guid OrderHeaderId { get; set; }
    public Guid OrderDetailId { get; set; }

    public string? TableName { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public string? ReasonSnapshot { get; set; }

    /// <summary>Silinmə sorğusunu göndərən (ofisiant/kassir adı).</summary>
    public string? RequestedByDisplayName { get; set; }

    /// <summary>Telegram sendMessage cavabı: chatId (string) → message_id JSON.</summary>
    public string? TelegramConfirmMessageRefsJson { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>0=Pending, 1=Accepted, 2=Rejected</summary>
    public byte Status { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
