namespace BusinessLayer.DTOs.Company;

public class CompanyPutDto
{
    public Guid Id { get; set; }
    public string NameAz { get; set; }
    public string? Logo { get; set; }
    public string AddressAz { get; set; }
    public string PhoneNumber1 { get; set; }
    public string? PhoneNumber2 { get; set; }
    public string? PhoneNumber3 { get; set; }
    public bool IsActive { get; set; }
    /// <summary>0 = Normal, 1 = Xəritə. Boşdursa mövcud dəyər saxlanılır.</summary>
    public int? TablesLayoutMode { get; set; }

    public bool EkassamEnabled { get; set; }
    public string? EkassamBaseUrl { get; set; }
    /// <summary>Boş göndərilərsə mövcud açar saxlanılır.</summary>
    public string? EkassamApiKey { get; set; }

    /// <summary>Boş göndərilərsə mövcud dəyər saxlanılır.</summary>
    public bool? IsGuestModeActive { get; set; }

    /// <summary>Kassa çekində son təşəkkür sətiri (boş = serverdə saxlanan / default).</summary>
    public string? KassaReceiptThankYouText { get; set; }

    /// <summary>true + yeni fayl yoxdursa: POS kilid ekranı şəkli silinir (fayl + DB).</summary>
    public bool ClearPosLockScreenImage { get; set; }

    /// <summary>true + yeni logo faylı yoxdursa: şirkət logosu silinir (fayl + DB).</summary>
    public bool ClearCompanyLogo { get; set; }
}