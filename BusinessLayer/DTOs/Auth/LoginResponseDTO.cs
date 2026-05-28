namespace BusinessLayer.DTOs.Auth;

public class LoginResponseDTO
{
    public Guid Id { get; set; }
    public string Token { get; set; }
    public string FullName { get; set; }
    public string RoleName { get; set; }
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; }
    public DateTime PackageEndDate { get; set; }

    public List<int> Permissions { get; set; }
    public bool RoleIsAdmin { get; set; }

    /// <summary>
    /// Eyni username ilə bir neçə restoran (company) olduqda Boss-da seçmək üçün.
    /// </summary>
    public List<UserCompanyBriefDTO> Companies { get; set; } = [];
}