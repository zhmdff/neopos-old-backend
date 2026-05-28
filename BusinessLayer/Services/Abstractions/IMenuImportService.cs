using BusinessLayer.DTOs.MenuImport;

namespace BusinessLayer.Services.Abstractions;

public interface IMenuImportService
{
    /// <summary>İki vərəq: Kateqoriyalar, Məhsullar — önizləmə və validasiya.</summary>
    Task<MenuImportPreviewResultDto> PreviewAsync(Stream excelStream, Guid companyId, CancellationToken cancellationToken = default);

    /// <summary>Eyni fayl strukturu ilə kateqoriya və məhsul yaradır (transaction).</summary>
    Task<MenuImportApplyResultDto> ApplyAsync(Stream excelStream, Guid companyId, CancellationToken cancellationToken = default);

    byte[] GetTemplateWorkbook();

    /// <summary>Mövcud kateqoriya və məhsulları import şablonu ilə eyni sütunlarda xlsx kimi qaytarır.</summary>
    Task<byte[]> ExportMenuWorkbookAsync(Guid companyId, CancellationToken cancellationToken = default);
}
