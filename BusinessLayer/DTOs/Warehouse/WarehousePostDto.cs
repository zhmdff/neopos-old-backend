namespace BusinessLayer.DTOs.Warehouse;

public class WarehousePostDto
{
    public string Name { get; set; }
    public string? Address { get; set; }
    public Guid CompanyId { get; set; }
    public bool IsDefaultSale { get; set; }
}
