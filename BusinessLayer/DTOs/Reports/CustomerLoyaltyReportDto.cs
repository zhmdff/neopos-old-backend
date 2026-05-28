namespace BusinessLayer.DTOs.Reports;

public class CustomerLoyaltyReportDto
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public int TotalCustomers { get; set; }

    public List<CustomerLoyaltyItemDto> Items { get; set; } = new();
}

