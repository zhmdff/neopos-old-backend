namespace BusinessLayer.DTOs.Supplier;

public class SupplierGetDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string? Address { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Note { get; set; }
}
