namespace BusinessLayer.DTOs.Auth;

public class WaiterShiftLoginRequestDTO
{
    public Guid CompanyId { get; set; }
    /// <summary>Boş ola bilər (sadə QR). Dolu olduqda növbə kodu yoxlanır.</summary>
    public string? AccessCode { get; set; }
}
