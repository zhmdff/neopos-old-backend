namespace BusinessLayer.DTOs.MenuImport;

public class MenuImportApplyResultDto
{
    public int CategoriesCreated { get; set; }
    public int ProductsCreated { get; set; }
    public string Message { get; set; } = "";
}
