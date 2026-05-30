using BusinessLayer.DTOs.Kitchen;

namespace BusinessLayer.DTOs.Print;

public class RenderKitchenEscPosDto
{
    public Guid CompanyId { get; set; }
    public string WorkshopName { get; set; } = "";
    public string HallName { get; set; } = "";
    public string TableName { get; set; } = "";
    public string WaiterName { get; set; } = "";
    public string? OpenTime { get; set; }
    public string? BeepMode { get; set; }
    public List<KitchenPrintItemDto> Items { get; set; } = [];
}
