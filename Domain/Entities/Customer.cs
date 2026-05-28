using Domain.Common;

namespace Domain.Entities;

public class Customer : AuditableCompanyEntity
{
    public string FullName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string? Address { get; set; }
    public DateTime? BirthDay { get; set; }
}
