namespace BusinessLayer.DTOs.ProductSet;

public class ProductSetChoiceOptionGetDto
{
    public Guid ProductId { get; set; }
    public string ProductNameAz { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public int SortOrder { get; set; }
}
