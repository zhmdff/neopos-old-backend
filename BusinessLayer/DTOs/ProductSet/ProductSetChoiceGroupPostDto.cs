namespace BusinessLayer.DTOs.ProductSet;

public class ProductSetChoiceGroupPostDto
{
    public string NameAz { get; set; } = string.Empty;
    public int MinChoices { get; set; } = 1;
    public int MaxChoices { get; set; } = 1;
    public int SortOrder { get; set; }
    public List<ProductSetChoiceOptionPostDto> Options { get; set; } = [];
}
