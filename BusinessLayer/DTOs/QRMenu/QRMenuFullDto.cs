namespace BusinessLayer.DTOs.QRMenu;

public class QRMenuFullDto
{
    // Company info
    public string Name { get; set; }
    public string? Logo { get; set; }
    public string Address { get; set; }
    public string Slug { get; set; }
    public string Phone1 { get; set; }
    public string? Phone2 { get; set; }
    public string? Phone3 { get; set; }

    public QRMenuSettingDto Settings { get; set; }

    public List<CategoryQRDto> Categories { get; set; }
}


