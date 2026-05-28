namespace BusinessLayer.DTOs.Purchase;

public class PurchaseItemPostDto
{
    public Guid ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal PriceAtPurchase { get; set; }
    public Guid? WarehouseId { get; set; }
}