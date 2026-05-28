namespace BusinessLayer.DTOs.Product;

public class DeletedProductReportItemDto
{
    public Guid Id { get; set; }
    public string NameAz { get; set; } = "";
    public string? CategoryName { get; set; }
    public string? WorkshopName { get; set; }
    public decimal SalePrice { get; set; }
    /// <summary>Silinmə vaxtı; köhnə qeydlərdə yalnız LastModifiedAt ola bilər.</summary>
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
}

/// <summary>
/// Sifarişdə məhsul sətirinin ləğvi — admin bildirişi / AuditLogs ilə eyni mənbə.
/// </summary>
public class OrderLineDeletionItemDto
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string UserName { get; set; } = "";
    public string TableName { get; set; } = "";
    public string? HallName { get; set; }
    public string Description { get; set; } = "";
    public string? LineProductName { get; set; }
    public decimal? LineQuantity { get; set; }
    public decimal? LineUnitPrice { get; set; }
    public decimal? LineTotal { get; set; }
}

public class DeletedProductsReportDto
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public int TotalCount { get; set; }
    /// <summary>Menyudan silinmiş məhsul kartları (Products.IsDeleted).</summary>
    public List<DeletedProductReportItemDto> Items { get; set; } = new();
    /// <summary>Sifarişdən ləğv — audit: «MƏHSUL SİLİNDİ».</summary>
    public List<OrderLineDeletionItemDto> OrderLineDeletions { get; set; } = new();
}
