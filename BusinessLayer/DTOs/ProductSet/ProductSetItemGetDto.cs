namespace BusinessLayer.DTOs.ProductSet;

public class ProductSetItemGetDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } // Tərkibdəki məhsulun adı (Məs: Lüle)
    public double Quantity { get; set; } // Miqdarı (Məs: 4 ədəd)
}