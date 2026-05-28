namespace BusinessLayer.DTOs.MenuImport;

public class MenuImportCategoryPreviewDto
{
    public string NameAz { get; set; } = "";
    public string? ParentName { get; set; }
    /// <summary>Yerli bazada artıq var.</summary>
    public bool AlreadyExists { get; set; }
    /// <summary>Fayldan import zamanı yaradılacaq.</summary>
    public bool WillBeCreated { get; set; }
}
