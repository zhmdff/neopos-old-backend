namespace BusinessLayer.DTOs.Reports;

public class ShiftReportDto : SummaryReportDto
{
    public Guid ShiftId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string OpenedBy { get; set; }

    /// <summary>Növbə üzrə daxili xərclər cəmi.</summary>
    public decimal ShiftExpensesTotal { get; set; }

    /// <summary>Növbə açılışında qeyd olunan kassa depoziti (çek depoziti deyil).</summary>
    public decimal OpeningDepositAmount { get; set; }
}
