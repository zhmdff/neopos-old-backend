namespace BusinessLayer.DTOs.OrderHeader;

public class OrderHeaderPostDto
{
    public Guid TableId { get; set; }
    public Guid CompanyId { get; set; }
    public string? CreatedBy { get; set; }
    public string? Note { get; set; }
    public Guid? ClientOrderId { get; set; }

    /// <summary>Terminal: masa açılan kimi qonaq sayı (opsional).</summary>
    public int? GuestCount { get; set; }
}