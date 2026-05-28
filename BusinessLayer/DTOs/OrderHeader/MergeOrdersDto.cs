namespace BusinessLayer.DTOs.OrderHeader;

public class MergeOrdersDto
{
    /// <summary>Hazırkı (cari) masanın aktiv çeki — bura məhsullar əlavə olunacaq.</summary>
    public Guid TargetOrderId { get; set; }

    /// <summary>Birləşdiriləcək ikinci masanın aktiv çeki (boşalacaq).</summary>
    public Guid SourceOrderId { get; set; }
}
