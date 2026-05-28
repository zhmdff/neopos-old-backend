using Domain.Common;
using Domain.Common.Entities;

namespace Domain.Entities;

/// <summary>
/// Zal üzrə vaxt pəncərəsində masa açılanda avtomatik endirim (məs. 18:00–23:00 → 20%).
/// </summary>
public class HallTimeDiscountRule : AuditableCompanyEntity
{
    public Guid HallId { get; set; }
    public Hall Hall { get; set; } = null!;

    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }

    public bool IsPercentageDiscount { get; set; }
    public decimal DiscountPercentage { get; set; }
    public decimal DiscountAmount { get; set; }

    public bool IsEnabled { get; set; } = true;
    public string? Label { get; set; }
}
