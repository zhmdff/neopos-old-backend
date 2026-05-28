namespace BusinessLayer.DTOs.OrderDetail;

public class OrderDetailUpdateDto
{
    public double Quantity { get; set; }
    public string? ItemNote { get; set; }
    public decimal? Price { get; set; }        
    public string? ProductName { get; set; }   
    /// <summary>
    /// Məhsul ləğv/silinmə səbəbi (audit üçün). ItemNote-a yazılmır.
    /// </summary>
    public string? CancelReason { get; set; }
    public Guid CompanyId { get; set; }
}