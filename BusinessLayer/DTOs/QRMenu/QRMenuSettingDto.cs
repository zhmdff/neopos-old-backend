namespace BusinessLayer.DTOs.QRMenu;

public class QRMenuSettingDto
{
    public string? WifiName { get; set; }
    public string? WifiPassword { get; set; }
    public string? InstagramUrl { get; set; }
    public string? TiktokUrl { get; set; }
    public string? FacebookUrl { get; set; }
    public bool Phone1HasWhatsApp { get; set; }
    public bool Phone2HasWhatsApp { get; set; }
    public bool Phone3HasWhatsApp { get; set; }
    public string? WorkingHours { get; set; }
    public string? MapLocationUrl { get; set; }
    public decimal ServiceChargePercent { get; set; }
    public List<string> GalleryImages { get; set; } = new();
}
