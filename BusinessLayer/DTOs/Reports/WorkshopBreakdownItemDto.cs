namespace BusinessLayer.DTOs.Reports;

/// <summary>
/// Satış məhsulunun əsas sexi (Mətbəx, Bar, Qəlyan və s.) üzrə cəm.
/// </summary>
public class WorkshopBreakdownItemDto
{
    public Guid WorkshopId { get; set; }
    public string WorkshopName { get; set; } = "";
    public decimal Revenue { get; set; }
}
