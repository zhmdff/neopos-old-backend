namespace BusinessLayer.DTOs.OrderDetail;

public class OrderDetailPostDto
{
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public double Quantity { get; set; }
    public string? ItemNote { get; set; }

    /// <summary>Set / Business Lunch tərkibi (mətbəx çapı; ItemNote-dan ayrı).</summary>
    public string? KitchenCompositionNote { get; set; }
}