namespace BusinessLayer.DTOs.MenuImport;

public class MenuImportPreviewResultDto
{
    public List<MenuImportCategoryPreviewDto> Categories { get; set; } = [];
    public List<MenuImportProductPreviewDto> Products { get; set; } = [];
    public List<string> GeneralErrors { get; set; } = [];
    public bool IsValid => !GeneralErrors.Any() && Products.All(p => string.IsNullOrEmpty(p.Error));
}
