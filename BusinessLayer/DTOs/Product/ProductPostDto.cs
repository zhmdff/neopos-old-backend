using Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace BusinessLayer.DTOs.Product;

public class ProductPostDto
{
    public string NameAz { get; set; }
    public string? Barcode { get; set; }
    public decimal CostPrice { get; set; }
    public decimal MarkupValue { get; set; }
    public MarkupType MarkupType { get; set; }
    public string? CookingProcess { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid WorkshopId { get; set; }
    public Guid CompanyId { get; set; }
    public IFormFile? ImageFile { get; set; }
    public SalesUnit Unit { get; set; }
    public decimal? DeliveryPrice { get; set; }

    /// <summary>
    /// Əlavə şöbələr (mətbəx çapı üçün). Primary şöbə: WorkshopId.
    /// </summary>
    public List<Guid> AdditionalWorkshopIds { get; set; } = new();

    /// <summary>
    /// Boss-da seçilməsə belə default true qalması üçün nullable saxlanılır.
    /// </summary>
    public bool? ShowInQr { get; set; }

    /// <summary>
    /// Boss-da seçilməsə belə default true qalması üçün nullable saxlanılır.
    /// </summary>
    public bool? ShowInTerminal { get; set; }
}