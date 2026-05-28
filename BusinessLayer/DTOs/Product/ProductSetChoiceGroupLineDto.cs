namespace BusinessLayer.DTOs.Product;

public class ProductSetChoiceGroupLineDto
{
    public string NameAz { get; set; } = string.Empty;
    public int MinChoices { get; set; }
    public int MaxChoices { get; set; }
    public int SortOrder { get; set; }
    public List<ProductSetChoiceOptionLineDto> Options { get; set; } = [];
}
