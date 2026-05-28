namespace BusinessLayer.DTOs.Reports;

public class TableBreakdownItemDto
{
    public Guid TableId { get; set; }
    public string TableName { get; set; } = "";
    public string HallName { get; set; } = "";

    public decimal Revenue { get; set; }
    public decimal Cash { get; set; }
    public decimal Card { get; set; }
    public int OrderCount { get; set; }
    public decimal AvgTicket => OrderCount <= 0 ? 0 : Revenue / OrderCount;

    public decimal OpenRevenueAdded { get; set; }
    public int OpenOrderCount { get; set; }
}

