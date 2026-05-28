namespace BusinessLayer.DTOs.Audit;

public class AuditLogPostDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } 
    public string Action { get; set; } 
    public string TableName { get; set; }
    public string? HallName { get; set; }
    public string Description { get; set; } 
    public Guid CompanyId { get; set; }
    public string CreatedBy { get; set; }

    /// <summary>Səbətdən silinən məhsul sətiri (opsional).</summary>
    public string? LineProductName { get; set; }
    public decimal? LineQuantity { get; set; }
    public decimal? LineUnitPrice { get; set; }
    public decimal? LineTotal { get; set; }
}