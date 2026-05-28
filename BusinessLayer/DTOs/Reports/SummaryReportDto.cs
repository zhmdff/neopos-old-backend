namespace BusinessLayer.DTOs.Reports;

public class SummaryReportDto
{
    public decimal TotalRevenue { get; set; }
    public decimal TotalCost { get; set; }
    public decimal TotalProfit => TotalRevenue - TotalCost;
    public decimal TotalCash { get; set; }
    public decimal TotalCard { get; set; }

    /// <summary>Əlavə ödəniş üsulları üzrə cəmlər (məs. Wolt). Bu çeklər nağd/kart ümumi cəmlərinə daxil edilmir.</summary>
    public List<CustomPaymentTotalDto> CustomPaymentTotals { get; set; } = new();
    /// <summary>Bağlanmış sifarişlər (dövr üzrə).</summary>
    public int OrderCount { get; set; }

    public decimal ClosedRevenue { get; set; }
    public int ClosedOrderCount { get; set; }
    public bool OpenTablesIncluded { get; set; }
    public decimal OpenRevenueAdded { get; set; }
    public decimal OpenCostAdded { get; set; }
    public decimal OpenCashAdded { get; set; }
    public decimal OpenCardAdded { get; set; }
    public int OpenOrderCount { get; set; }

    /// <summary>Bağlı (+ istənilən açıq) sifarişlərdə xidmət haqqı cəmi.</summary>
    public decimal ServiceFeeRevenue { get; set; }
    /// <summary>Bağlı (+ istənilən açıq) sifarişlərdə depozit məbləği cəmi.</summary>
    public decimal DepositRevenue { get; set; }

    /// <summary>Bağlı (+ istənilən açıq) çeklərdə tətbiq olunmuş endirim cəmi.</summary>
    public decimal TotalDiscountAmount { get; set; }

    public List<DailyReportItemDto> DailyReports { get; set; } = new();
}
