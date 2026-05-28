namespace Domain.Common.Entities;

public class Supplier : AuditableCompanyEntity
{
    public string Name { get; set; }
    public string? Address { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Note { get; set; }
}
