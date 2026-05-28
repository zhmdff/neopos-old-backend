using Domain.Enums;

namespace Domain.Common.Entities;

public class Product : AuditableCompanyEntity
{
    public string NameAz { get; set; }
    public string NameEn { get; set; }
    public string NameRu { get; set; }

    public string? Barcode { get; set; }
    public int OrderIndex { get; set; }
    public int? OrderIndexByQrMenu { get; set; }

    public SalesUnit Unit { get; set; }

    public decimal Stock { get; set; } = 0;
    public decimal CostPrice { get; set; } 
    public decimal MarkupValue { get; set; } 
    public MarkupType MarkupType { get; set; } 

    public decimal SalePrice { get; set; }
    public decimal? DeliveryPrice { get; set; }
    public string? ImageUrl { get; set; }

    /// <summary>
    /// QR menyuda görünsün?
    /// Default: true
    /// </summary>
    public bool ShowInQr { get; set; } = true;

    /// <summary>
    /// Terminal (POS) menyuda görünsün?
    /// Default: true
    /// </summary>
    public bool ShowInTerminal { get; set; } = true;

    /// <summary>Null: kateqoriyasız — terminalda kök menyuda kateqoriya kartlarının yanında göstərilir.</summary>
    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }

    public Guid WorkshopId { get; set; }
    public Workshop Workshop { get; set; }

    /// <summary>
    /// Əlavə sexlər (mətbəx çapı üçün). Əsas sex: WorkshopId.
    /// </summary>
    public ICollection<ProductWorkshop> AdditionalWorkshops { get; set; } = [];

    public string? CookingProcess { get; set; }

    public ICollection<ProductVariant> Variants { get; set; } = [];
}
