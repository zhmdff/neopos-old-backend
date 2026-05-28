namespace BusinessLayer.DTOs.Auth;

public class PinLoginRequestDTO
{
    public Guid CompanyId { get; set; }
    public string PinCode { get; set; } = null!;
}