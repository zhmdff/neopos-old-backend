namespace BusinessLayer.DTOs.Company;

public class CompanyGetDto
{
    public Guid Id { get; set; }
    public string NameAz { get; set; }
    public string NameRu { get; set; }
    public string NameEn { get; set; }
    public string? Logo { get; set; }
    public string AddressAz { get; set; }
    public string AddressRu { get; set; }
    public string AddressEn { get; set; }
    public string PhoneNumber1 { get; set; }
    public string? PhoneNumber2 { get; set; }
    public string? PhoneNumber3 { get; set; }
    public DateTime PackageEndDate { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsDeliveryPriceEnabled { get; set; } 
    public bool IsUserModeActive { get; set; }
    public bool IsGuestModeActive { get; set; }
    /// <summary>0 = Normal (şəbəkə), 1 = Xəritə</summary>
    public int TablesLayoutMode { get; set; }
    public string Slug { get; set; }

    public bool EkassamEnabled { get; set; }
    public string? EkassamBaseUrl { get; set; }
    public string? EkassamApiKey { get; set; }

    public bool AutoCashShiftEnabled { get; set; }
    public string AutoCashShiftOpenTime { get; set; } = "09:00";
    public string AutoCashShiftCloseTime { get; set; } = "23:00";
    public bool AutoCashShiftForceClose { get; set; } = true;

    public bool CashShiftPromptOpeningDeposit { get; set; }

    public bool CashShiftPrintReportOnClose { get; set; } = true;

    public string? CashierPrinterTarget { get; set; }
    public string? KitchenPrinterTarget { get; set; }
    public string? ReceiptDesignSettingsJson { get; set; }

    public string? KassaReceiptThankYouText { get; set; }
    public string? PosLockScreenImage { get; set; }
    public string? CustomerDisplayLockScreenImage { get; set; }

    /// <summary>Terminal menyu: şöbəyə görə kateqoriya/məhsul süzülməsi.</summary>
    public bool MenuFilterByWorkshop { get; set; }

    /// <summary>Terminalda silinmə təsdiqi (Telegram/Boss) aktivdirsə true.</summary>
    public bool TerminalLineDeleteConfirmEnabled { get; set; }
}