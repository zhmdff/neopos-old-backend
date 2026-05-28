namespace BusinessLayer.DTOs.QRMenu;

public class ProductQRDto
{
    /// <summary>Kataloq sətri ID (variant sətirində variant ID).</summary>
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public string NameAz { get; set; }
    public string NameRu { get; set; }
    public string NameEn { get; set; }
    public decimal SalePrice { get; set; }
    public string? ImageUrl { get; set; }
    public string? CookingProcess { get; set; }
    public int? OrderIndexByQrMenu { get; set; }
}