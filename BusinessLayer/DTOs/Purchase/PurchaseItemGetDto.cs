namespace BusinessLayer.DTOs.Purchase;

public class PurchaseItemGetDto
{
    public string ProductName { get; set; }
    public decimal Quantity { get; set; }
    public decimal PriceAtPurchase { get; set; }
    public decimal SubTotal => Quantity * PriceAtPurchase;
    public string WarehouseName { get; set; }
}
