namespace BusinessLayer.DTOs.Reports;

public class WaiterBreakdownItemDto
{
    public string WaiterName { get; set; } = "";

    public decimal Revenue { get; set; }
    public decimal Cash { get; set; }
    public decimal Card { get; set; }
    public int OrderCount { get; set; }
    public decimal AvgTicket => OrderCount <= 0 ? 0 : Revenue / OrderCount;

    public decimal OpenRevenueAdded { get; set; }
    public int OpenOrderCount { get; set; }
}

