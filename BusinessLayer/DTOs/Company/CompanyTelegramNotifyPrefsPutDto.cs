namespace BusinessLayer.DTOs.Company;

public class CompanyTelegramNotifyPrefsPutDto
{
    /// <summary>Terminal localStorage ilə eyni JSON obyekti (boolean prefs).</summary>
    public Dictionary<string, bool>? Prefs { get; set; }
}
