namespace BusinessLayer.DTOs.Reports;

public class ProductBreakdownItemDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string? CategoryName { get; set; }

    /// <summary>Çekdə məhsul variantı (məs: Çay + Çaydan → "Çay-Çaydan").</summary>
    public Guid? ProductVariantId { get; set; }
    public string? ProductVariantName { get; set; }

    public decimal Quantity { get; set; }
    public decimal Revenue { get; set; }

    public decimal Cost { get; set; }
    public decimal Profit => Revenue - Cost;
    public decimal ProfitMargin => Revenue <= 0 ? 0 : Profit / Revenue;
}

