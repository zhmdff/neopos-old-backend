namespace BusinessLayer.DTOs.ProductSet;

public class ProductSetPostDto
{
    public Guid ProductId { get; set; } 
    public string? Description { get; set; }

    public List<ProductSetItemPostDto>? SetItems { get; set; }

    /// <summary>Business lunch: seçim qrupları (hər qrupdan N seçim).</summary>
    public List<ProductSetChoiceGroupPostDto>? ChoiceGroups { get; set; }

    public Guid CompanyId { get; set; }
}
