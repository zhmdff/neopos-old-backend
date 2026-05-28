namespace BusinessLayer.DTOs.CashShift;

public class CashShiftCloseDto
{
    public Guid Id { get; set; }
    public Guid ClosedByUserId { get; set; }
    /// <summary>Terminal cədvəli ilə avtomatik bağlanış — audit üçün.</summary>
    public bool IsAutoSchedule { get; set; }
    /// <summary>Əl ilə: açıq məhsullu masalar olsa da növbəni bağlamağa icazə (təsdiq modalından sonra).</summary>
    public bool AllowCloseWithOpenTables { get; set; }
}