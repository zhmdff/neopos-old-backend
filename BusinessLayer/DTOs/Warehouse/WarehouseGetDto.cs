namespace BusinessLayer.DTOs.Warehouse;

public class WarehouseGetDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string? Address { get; set; }
    public bool IsDefaultSale { get; set; }
}
