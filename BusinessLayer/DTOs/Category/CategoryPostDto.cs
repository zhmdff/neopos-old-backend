using Microsoft.AspNetCore.Http;

namespace BusinessLayer.DTOs.Category;

public class CategoryPostDto
{
    public string NameAz { get; set; }
    public int OrderIndex { get; set; }
    public IFormFile? ImageFile { get; set; }
    public Guid? ParentCategoryId { get; set; }
    public Guid CompanyId { get; set; }
}
