namespace BusinessLayer.DTOs.PendingLineDelete;

public class PendingLineDeleteRegisterDto
{
    public string? PendingId { get; set; }
    public Guid OrderId { get; set; }
    public Guid OrderLineId { get; set; }
    public string? TableName { get; set; }
    public string? ProductName { get; set; }
    public double Quantity { get; set; }
    public string? ReasonSnapshot { get; set; }
    /// <summary>Silinmə sorğusunu göndərən (ad, Telegram mətnində).</summary>
    public string? RequestedByDisplayName { get; set; }
    /// <summary>Unix ms və ya ISO; boşdursa server 2 dəq əlavə edir.</summary>
    public DateTime? ExpiresAtUtc { get; set; }
}

public class PendingLineDeleteResolveDto
{
    public bool Accepted { get; set; }
}

public class PendingLineDeleteActiveItemDto
{
    public string PendingId { get; set; } = string.Empty;
    public Guid OrderId { get; set; }
    public Guid OrderLineId { get; set; }
    public string? TableName { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public string? ReasonSnapshot { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
