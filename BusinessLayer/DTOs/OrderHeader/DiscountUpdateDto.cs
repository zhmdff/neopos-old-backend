namespace BusinessLayer.DTOs.OrderHeader;

public class DiscountUpdateDto
{
    public decimal Value { get; set; }
    public bool IsPercentage { get; set; }
}