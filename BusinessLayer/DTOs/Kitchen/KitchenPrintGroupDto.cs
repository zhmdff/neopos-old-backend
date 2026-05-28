namespace BusinessLayer.DTOs.Kitchen;

public class KitchenPrintGroupDto
{
    public string WorkshopName { get; set; }
    public string PrinterType { get; set; }
    public string PrinterValue { get; set; }

    // Bu workshop-a aid olan bütün fərqlər
    public List<KitchenPrintItemDto> Items { get; set; } = new();
}

public class KitchenPrintItemDto
{
    public string Name { get; set; }
    public double Qty { get; set; }
    public string Status { get; set; } // Enum-dan gələn string ("YENI", "AZALDI", "LEGVE")
    public string Note { get; set; }
    /// <summary>Set / Business Lunch tərkibi (mətbəx slipində ayrıca sətir).</summary>
    public string? CompositionNote { get; set; }
    public double Total { get; set; }
}