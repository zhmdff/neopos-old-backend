namespace BusinessLayer.DTOs.Purchase;

public class PurchaseGetDto
{
    public Guid Id { get; set; }
    public string SupplierName { get; set; }
    public string WarehouseName { get; set; }
    public DateTime PurchaseDate { get; set; }
    public string InvoiceNumber { get; set; }
    public decimal TotalAmount { get; set; }

    public List<PurchaseItemGetDto> PurchaseItems { get; set; }
}