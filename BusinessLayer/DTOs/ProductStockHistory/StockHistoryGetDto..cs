namespace BusinessLayer.DTOs.ProductStockHistory;

public class StockHistoryGetDto
{
    public Guid Id { get; set; }
    public string ProductName { get; set; }
    public string WarehouseName { get; set; }
    public decimal QuantityBefore { get; set; }
    public decimal ChangeAmount { get; set; }
    public decimal QuantityAfter { get; set; }
    public string MovementTypeName { get; set; } // Enum-ın string adı
    public string? SupplierName { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } // Nə vaxt baş verib
}
