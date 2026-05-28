using BusinessLayer.DTOs.Product;

public interface IProductService
{
    // companyId əlavə edildi
    Task<IEnumerable<ProductGetDto>> GetAllAsync(Guid companyId, int skip, int take, string? search, Guid? categoryId, Guid? workshopId, bool uncategorizedOnly = false);
    Task<Guid> CreateAsync(ProductPostDto dto); // DTO içində companyId var
    Task UpdateAsync(ProductPutDto dto); // DTO içində companyId var
    Task DeleteAsync(Guid id, Guid companyId);
    Task UpdateOrdersAsync(Guid companyId, List<ProductOrderUpdateDto> dtos);
    Task<(IEnumerable<ProductStockStatusDto> items, int totalCount)> GetStockStatusAsync(Guid companyId, int skip, int take, string? search);

    Task<DeletedProductsReportDto> GetDeletedReportAsync(DateTime start, DateTime end, Guid companyId);
}