namespace BusinessLayer.DTOs.Customer;

public class CustomerGetDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string? Address { get; set; }
    public DateTime? BirthDay { get; set; }
}

public class CustomerPostDto
{
    public string FullName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string? Address { get; set; }
    public DateTime? BirthDay { get; set; }
}
