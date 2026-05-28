namespace BusinessLayer.DTOs.Company;

public class AutoCashShiftConfigPutDto
{
    public bool Enabled { get; set; }
    /// <summary>HH:mm (Bakı).</summary>
    public string OpenTime { get; set; } = "09:00";
    /// <summary>HH:mm (Bakı).</summary>
    public string CloseTime { get; set; } = "23:00";
    public bool ForceClose { get; set; } = true;

    /// <summary>Növbə əl ilə açılanda depozit məbləği soruşulsun.</summary>
    public bool PromptOpeningDeposit { get; set; }

    /// <summary>Növbə bağlananda sadə hesabat çapı (kassa printer).</summary>
    public bool PrintReportOnClose { get; set; } = true;
}
