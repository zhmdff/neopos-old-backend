namespace BusinessLayer.DTOs.ProductVariant;

public class ProductVariantGetDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string NameAz { get; set; }
    public string NameEn { get; set; }
    public string NameRu { get; set; }
    public decimal Price { get; set; }
    public decimal? DeliveryPrice { get; set; }
    public string? Barcode { get; set; }
    public int OrderIndex { get; set; }
}

