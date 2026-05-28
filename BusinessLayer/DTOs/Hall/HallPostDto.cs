namespace BusinessLayer.DTOs.Hall;

public class HallPostDto
{
    public string NameAz { get; set; }
    public decimal ServicePercentage { get; set; }
    public int OrderIndex { get; set; }
    public Guid CompanyId { get; set; }
    public bool IsGuestCountEnabled { get; set; } = true;
    public bool IsTableHourActive { get; set; }
}