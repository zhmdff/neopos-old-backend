namespace BusinessLayer.DTOs.MenuImport;

public class MenuImportProductPreviewDto
{
    public int ExcelRowNumber { get; set; }
    public string NameAz { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public string WorkshopName { get; set; } = "";
    public decimal? CostPrice { get; set; }
    public decimal SalePrice { get; set; }
    public string? Barcode { get; set; }
    public string UnitLabel { get; set; } = "Ədəd";
    /// <summary>Boşdursa sətir import üçün yararlıdır.</summary>
    public string? Error { get; set; }
}
