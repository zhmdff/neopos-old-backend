namespace Domain.Common.Entities;

/// <summary>Kassa növbəsi üzrə restoranın daxili xərcləri (market, təmir və s.).</summary>
public class CashShiftExpense : AuditableCompanyEntity
{
    public Guid CashShiftId { get; set; }
    public virtual CashShift CashShift { get; set; } = null!;

    public decimal Amount { get; set; }

    /// <summary>Qısa təsvir (məs: «Məişət üçün market»).</summary>
    public string Note { get; set; } = "";

    public Guid? RecordedByUserId { get; set; }
    public virtual User? RecordedByUser { get; set; }
}
