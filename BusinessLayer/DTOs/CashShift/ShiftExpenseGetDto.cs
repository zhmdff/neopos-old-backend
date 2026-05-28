namespace BusinessLayer.DTOs.CashShift;

public class ShiftExpenseGetDto
{
    public Guid Id { get; set; }
    public Guid CashShiftId { get; set; }
    public decimal Amount { get; set; }
    public string Note { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public string? RecordedByUserName { get; set; }

    public DateTime ShiftStartTime { get; set; }
    public DateTime? ShiftEndTime { get; set; }
    public bool ShiftIsClosed { get; set; }
}
