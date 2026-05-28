namespace BusinessLayer.DTOs.ProductSet;

public class ProductSetChoiceGroupGetDto
{
    public string NameAz { get; set; } = string.Empty;
    public int MinChoices { get; set; }
    public int MaxChoices { get; set; }
    public int SortOrder { get; set; }
    public List<ProductSetChoiceOptionGetDto> Options { get; set; } = [];
}
