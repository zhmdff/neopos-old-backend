namespace BusinessLayer.DTOs.Purchase;

public class PurchasePostDto
{
    public Guid CompanyId { get; set; }
    public Guid SupplierId { get; set; }
    public Guid WarehouseId { get; set; }
    public DateTime PurchaseDate { get; set; }
    public string? InvoiceNumber { get; set; }

    public List<PurchaseItemPostDto> Items { get; set; } = new();
}