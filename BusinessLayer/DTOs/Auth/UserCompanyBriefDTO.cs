namespace BusinessLayer.DTOs.Auth;

public class UserCompanyBriefDTO
{
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    /// <summary>Boss şirkət seçicisində göstərmək üçün (NameEn).</summary>
    public string CompanyNameEn { get; set; } = string.Empty;
    public DateTime PackageEndDate { get; set; }
}

