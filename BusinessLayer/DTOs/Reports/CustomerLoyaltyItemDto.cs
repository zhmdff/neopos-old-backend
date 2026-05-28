namespace BusinessLayer.DTOs.Reports;

public class CustomerLoyaltyItemDto
{
    public Guid CustomerId { get; set; }
    public string FullName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string? Address { get; set; }

    public decimal TotalSpent { get; set; }
    public int OrderCount { get; set; }
    public decimal AvgTicket => OrderCount <= 0 ? 0 : TotalSpent / OrderCount;

    public DateTime? LastOrderAt { get; set; }
    public decimal LastOrderTotal { get; set; }
}

