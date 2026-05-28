namespace BusinessLayer.DTOs.CashShift;

public class CashShiftGetDto
{
    public Guid Id { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string OpenedByUserName { get; set; }
    public string? ClosedByUserName { get; set; }
    public bool IsClosed { get; set; }

    public decimal TotalCash { get; set; }
    public decimal TotalCard { get; set; }
    public decimal TotalRevenue { get; set; }
    public int OrderCount { get; set; }

    /// <summary>Aktiv növbənin ofisiant PIN kodu (yalnız kassa terminalları göstərir).</summary>
    public string? WaiterAccessCode { get; set; }

    public decimal OpeningDepositAmount { get; set; }
}