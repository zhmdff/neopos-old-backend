namespace Domain.Common.Entities;

/// <summary>
/// Məhsulun əlavə sexləri (mətbəx çapı üçün). Primary sex: Product.WorkshopId.
/// </summary>
public class ProductWorkshop : AuditableCompanyEntity
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; }

    public Guid WorkshopId { get; set; }
    public Workshop Workshop { get; set; }
}

