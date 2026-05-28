namespace Domain.Common.Entities;

public class Purchase : AuditableCompanyEntity
{
    public Guid SupplierId { get; set; }
    public Supplier Supplier { get; set; }

    public Guid WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; }

    public DateTime PurchaseDate { get; set; }
    public string? InvoiceNumber { get; set; } // Qaimə nömrəsi (opsional)
    public decimal TotalAmount { get; set; } // Bütün məhsulların cəmi məbləği

    // Alt sətirlər ilə əlaqə (Toplu seçim üçün)
    public ICollection<PurchaseItem> PurchaseItems { get; set; } = new List<PurchaseItem>();
}