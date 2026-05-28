namespace BusinessLayer.DTOs.OrderHeader;

/// <summary>Növbə tarixçəsindən çeki yeniləyərkən isteğe bağlı səbəb (terminal modalı).</summary>
public class ReopenArchiveOrderDto
{
    /// <summary>wrong_close | wrong_product | customer</summary>
    public string? PresetKey { get; set; }
    public string? Note { get; set; }
}
