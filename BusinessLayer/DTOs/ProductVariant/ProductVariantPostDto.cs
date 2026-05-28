namespace BusinessLayer.DTOs.ProductVariant;

public class ProductVariantPostDto
{
    public Guid ProductId { get; set; }
    public Guid CompanyId { get; set; }
    public string NameAz { get; set; }
    public decimal Price { get; set; }
    public decimal? DeliveryPrice { get; set; }
    public string? Barcode { get; set; }
}

