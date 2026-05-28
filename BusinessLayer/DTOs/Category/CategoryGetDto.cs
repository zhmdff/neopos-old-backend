namespace BusinessLayer.DTOs.Category;

public class CategoryGetDto
{
    public Guid Id { get; set; }
    public string NameAz { get; set; }
    public string NameEn { get; set; }
    public string NameRu { get; set; }
    public int OrderIndex { get; set; }
    public string? ImageUrl { get; set; }
    public Guid? ParentCategoryId { get; set; }
    public int SubCategoryCount { get; set; }
    public int ProductCount { get; set; }

    public List<CategoryGetDto> SubCategories { get; set; } = [];
}
