namespace BusinessLayer.DTOs.ProductSet;

public class ProductSetChoiceOptionPostDto
{
    public Guid ProductId { get; set; }
    public double Quantity { get; set; } = 1;
    public int SortOrder { get; set; }
}
