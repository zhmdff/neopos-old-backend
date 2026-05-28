using Domain.Enums;

namespace Domain.Common.Entities;

public class ProductStockHistory : AuditableCompanyEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; }

    public Guid WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; }

    public decimal QuantityBefore { get; set; }
    public decimal ChangeAmount { get; set; }   // Müsbət (+2.5) və ya mənfi (-1.0)
    public decimal QuantityAfter { get; set; }

    public StockMovementType MovementType { get; set; }

    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public string? Note { get; set; } // Əlavə qeydlər üçün
}