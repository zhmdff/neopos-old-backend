using BusinessLayer.DTOs.Workshop;
using BusinessLayer.DTOs.ProductVariant;
using Domain.Enums;

namespace BusinessLayer.DTOs.Product;

public class ProductGetDto
{
    public Guid Id { get; set; }
    public string NameAz { get; set; }
    public string NameEn { get; set; }
    public string NameRu { get; set; }
    public string? Barcode { get; set; }
    public int OrderIndex { get; set; }
    public SalesUnit Unit { get; set; }
    public string UnitName => Unit.ToString();
    public decimal CostPrice { get; set; }
    public decimal MarkupValue { get; set; }
    public MarkupType MarkupType { get; set; }
    public decimal SalePrice { get; set; }
    public string? ImageUrl { get; set; }
    public string? CookingProcess { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public Guid WorkshopId { get; set; }
    public string WorkshopName { get; set; }
    public WorkshopGetDto Workshop { get; set; }
    public List<Guid> AdditionalWorkshopIds { get; set; } = [];
    public decimal? DeliveryPrice { get; set; }
    public List<ProductVariantGetDto> Variants { get; set; } = [];

    public bool ShowInQr { get; set; }
    public bool ShowInTerminal { get; set; }

    /// <summary>Boss-da set (ProductSet) təyin olunubsa — tərkib sətirləri.</summary>
    public string? SetDescription { get; set; }
    public List<ProductSetCompositionLineDto> SetComposition { get; set; } = [];

    /// <summary>Business lunch — seçim qrupları (terminal seçim modalı üçün).</summary>
    public List<ProductSetChoiceGroupLineDto> SetChoiceGroups { get; set; } = [];

    /// <summary>
    /// Boss üçün: məhsul business lunch kimi qurulubmu?
    /// (yəni SetChoiceGroups doludur)
    /// </summary>
    public bool HasBusinessLunch { get; set; }
}