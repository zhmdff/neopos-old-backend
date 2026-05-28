using BusinessLayer.DTOs.Product;

namespace BusinessLayer.DTOs.OrderDetail;

public class OrderDetailGetDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public string? ProductVariantName { get; set; }
    public string? ProductName { get; set; }
    public decimal Price { get; set; }
    public double Quantity { get; set; }
    public decimal TotalPrice { get; set; }
    public string? ItemNote { get; set; }
    public string? KitchenCompositionNote { get; set; }
    public bool IsSent { get; set; }

    /// <summary>KitchenOperations üzrə bu sətir üçün mətbəxə hesablanmış net miqdar (≥0).</summary>
    public double KitchenSentQuantity { get; set; }
    /// <summary>0 — təyin yoxdur; 1+ parça nömrəsi.</summary>
    public int SplitGroup { get; set; }
    /// <summary>Terminal masa saat limiti: ilk məhsul vaxtı (bütün cihazlarda eyni timer).</summary>
    public DateTime CreatedAt { get; set; }
    public ProductGetDto Product { get; set; }
}
