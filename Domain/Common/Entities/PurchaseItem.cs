namespace Domain.Common.Entities;

public class PurchaseItem : AuditableCompanyEntity
{
    public Guid PurchaseId { get; set; }
    public Purchase Purchase { get; set; }

    public Guid ProductId { get; set; }
    public Product Product { get; set; }

    public decimal Quantity { get; set; }
    public decimal PriceAtPurchase { get; set; }

    public Guid? WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
}