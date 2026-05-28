namespace BusinessLayer.DTOs.Reports;

public class BreakdownReportDto<TItem>
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public bool OpenTablesIncluded { get; set; }

    public decimal TotalRevenue { get; set; }
    public decimal TotalCash { get; set; }
    public decimal TotalCard { get; set; }
    public int OrderCount { get; set; }

    public decimal OpenRevenueAdded { get; set; }
    public decimal OpenCashAdded { get; set; }
    public decimal OpenCardAdded { get; set; }
    public int OpenOrderCount { get; set; }

    public List<TItem> Items { get; set; } = new();
}

