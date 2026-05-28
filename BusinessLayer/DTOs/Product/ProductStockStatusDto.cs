namespace BusinessLayer.DTOs.Product;

public class ProductStockStatusDto
{
    public Guid Id { get; set; }
    public string NameAz { get; set; }
    public decimal Stock { get; set; }        // Ümumi Stok
    public decimal CostPrice { get; set; }
    public string UnitName { get; set; }
    public List<WarehouseStockDto> WarehouseDetails { get; set; }
}

public class WarehouseStockDto
{
    public string WarehouseName { get; set; }
    public decimal Quantity { get; set; }
}