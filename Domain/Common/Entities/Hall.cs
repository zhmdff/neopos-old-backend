namespace Domain.Common.Entities;

public class Hall : AuditableCompanyEntity
{
    public string NameAz { get; set; }
    public string NameEn { get; set; }
    public string NameRu { get; set; }

    public decimal ServicePercentage { get; set; }
    public int OrderIndex { get; set; }
    /// <summary>
    /// Qonaq sayı modalı bu zalda soruşulsun?
    /// </summary>
    public bool IsGuestCountEnabled { get; set; } = true;
    /// <summary>Bu zalda masa saat limiti aktivdir (hər masanın öz limiti).</summary>
    public bool IsTableHourActive { get; set; }
    public ICollection<Table> Tables { get; set; } = new List<Table>();
}
