namespace BusinessLayer.DTOs.CashShift;

public class ShiftExpensePostDto
{
    public Guid CompanyId { get; set; }
    public decimal Amount { get; set; }
    public string? Note { get; set; }
}
