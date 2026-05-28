namespace BusinessLayer.DTOs.Company;

public class CompanyReceiptDesignPutDto
{
    public string? CashierPrinterTarget { get; set; }
    public string? KitchenPrinterTarget { get; set; }

    /// <summary>
    /// JSON string — sərbəst saxlanılır (frontend shape dəyişə bilər).
    /// </summary>
    public string? ReceiptDesignSettingsJson { get; set; }

    /// <summary>
    /// Kassa çekinin son təşəkkür sətiri. JSON-da göndərilməyibsə mövcud dəyər dəyişməz.
    /// </summary>
    public string? KassaReceiptThankYouText { get; set; }
}

