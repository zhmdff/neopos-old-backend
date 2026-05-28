
namespace BusinessLayer.DTOs.Supplier;

public class SupplierPostDto
{
    public string Name { get; set; }
    public string? Address { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Note { get; set; }
    public Guid CompanyId { get; set; }
}
