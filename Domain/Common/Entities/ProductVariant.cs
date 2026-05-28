namespace Domain.Common.Entities;

public class ProductVariant : AuditableCompanyEntity
{
    public string NameAz { get; set; }
    public string NameEn { get; set; }
    public string NameRu { get; set; }

    /// <summary>
    /// Variant üçün satış qiyməti (məs: 50qr, 100qr və s.).
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>Çöl / takeaway masalarında istifadə olunan qiymət (şirkətdə aktivdirsə).</summary>
    public decimal? DeliveryPrice { get; set; }

    public string? Barcode { get; set; }

    public int OrderIndex { get; set; }

    public Guid ProductId { get; set; }
    public Product Product { get; set; }
}

