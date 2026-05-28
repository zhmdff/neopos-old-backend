namespace BusinessLayer.DTOs.CashShift;

public class CashShiftOpenDto
{
    public Guid CompanyId { get; set; }
    public Guid OpenedByUserId { get; set; }
    /// <summary>Terminal cədvəli ilə avtomatik açılış — audit üçün.</summary>
    public bool IsAutoSchedule { get; set; }

    /// <summary>Əl ilə açılışda kassa depoziti (≥0). Avtomatik cədvəldə server 0 qəbul edir.</summary>
    public decimal OpeningDepositAmount { get; set; }
}