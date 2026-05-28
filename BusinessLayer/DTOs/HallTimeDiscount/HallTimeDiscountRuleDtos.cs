namespace BusinessLayer.DTOs.HallTimeDiscount;

public class HallTimeDiscountRuleGetDto
{
    public Guid Id { get; set; }
    public Guid HallId { get; set; }
    public string StartTime { get; set; } = "00:00";
    public string EndTime { get; set; } = "23:59";
    public bool IsPercentageDiscount { get; set; }
    public decimal DiscountPercentage { get; set; }
    public decimal DiscountAmount { get; set; }
    public bool IsEnabled { get; set; }
    public string? Label { get; set; }
}

public class HallTimeDiscountRulePostDto
{
    public Guid HallId { get; set; }
    public Guid CompanyId { get; set; }
    public string StartTime { get; set; } = "18:00";
    public string EndTime { get; set; } = "23:59";
    public bool IsPercentageDiscount { get; set; } = true;
    public decimal DiscountPercentage { get; set; }
    public decimal DiscountAmount { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string? Label { get; set; }
}

public class HallTimeDiscountRulePutDto : HallTimeDiscountRulePostDto
{
    public Guid Id { get; set; }
}
