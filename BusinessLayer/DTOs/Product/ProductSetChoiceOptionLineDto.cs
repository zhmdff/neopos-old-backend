namespace BusinessLayer.DTOs.Product;

public class ProductSetChoiceOptionLineDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public int SortOrder { get; set; }
}
