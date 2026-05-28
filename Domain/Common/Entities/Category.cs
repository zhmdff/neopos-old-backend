namespace Domain.Common.Entities;


public class Category : AuditableCompanyEntity
{
    public string NameAz { get; set; }
    public string NameEn { get; set; }
    public string NameRu { get; set; }

    public int OrderIndex { get; set; }
    public int? OrderIndexByQrMenu { get; set; }


    public string? ImageUrl { get; set; }

    public Guid? ParentCategoryId { get; set; }
    public Category? ParentCategory { get; set; }

    public ICollection<Category> SubCategories { get; set; } = [];

    public ICollection<Product> Products { get; set; } = [];
}