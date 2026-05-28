using BusinessLayer.DTOs.Table;

namespace BusinessLayer.DTOs.Hall;

public class HallGetDto
{
    public Guid Id { get; set; }
    public string NameAz { get; set; }
    public string NameEn { get; set; }
    public string NameRu { get; set; }
    public decimal ServicePercentage { get; set; }
    public int OrderIndex { get; set; }
    public bool IsGuestCountEnabled { get; set; }
    public bool IsTableHourActive { get; set; }
    public int TableCount { get; set; }
    public List<TableGetDto> Tables { get; set; } = [];
}