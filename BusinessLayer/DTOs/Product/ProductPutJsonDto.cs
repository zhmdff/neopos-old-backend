using Domain.Enums;

namespace BusinessLayer.DTOs.Product;

/// <summary>
/// Şəkilsiz məhsul yeniləməsi (application/json). Tex kart / API klientləri üçün.
/// </summary>
public class ProductPutJsonDto
{
    public Guid Id { get; set; }
    public string NameAz { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public decimal CostPrice { get; set; }
    public decimal MarkupValue { get; set; }
    public MarkupType MarkupType { get; set; }
    public string? CookingProcess { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid WorkshopId { get; set; }
    public Guid CompanyId { get; set; }
    public SalesUnit Unit { get; set; }
    public decimal? DeliveryPrice { get; set; }

    public List<Guid> AdditionalWorkshopIds { get; set; } = new();

    public bool? ShowInQr { get; set; }
    public bool? ShowInTerminal { get; set; }
}
