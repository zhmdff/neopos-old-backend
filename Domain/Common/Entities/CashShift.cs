namespace Domain.Common.Entities;

public class CashShift : AuditableCompanyEntity
{
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    public Guid OpenedByUserId { get; set; }
    public User OpenedByUser { get; set; }

    public Guid? ClosedByUserId { get; set; }
    public User ClosedByUser { get; set; }

    public bool IsClosed { get; set; } = false;

    /// <summary>Növbə açılışında kassaya qoyulan depozit (≥0).</summary>
    public decimal OpeningDepositAmount { get; set; }

    /// <summary>Aktiv növbə üçün ofisiant veb giriş kodu (6 rəqəm).</summary>
    public string? WaiterAccessCode { get; set; }

    public virtual ICollection<CashShiftExpense> Expenses { get; set; } = [];
}
