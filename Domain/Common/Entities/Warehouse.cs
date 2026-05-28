namespace Domain.Common.Entities;

public class Warehouse : AuditableCompanyEntity
{
    public string Name { get; set; }
    public string? Address { get; set; }
    public bool IsDefaultSale { get; set; } = false;
}
