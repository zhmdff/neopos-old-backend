using BusinessLayer.DTOs.Category;

namespace BusinessLayer.Services.Abstractions;

public interface ICategoryService
{
    Task<IEnumerable<CategoryGetDto>> GetAllAsync(Guid companyId, int skip, int take, string? search, Guid? parentId = null);
    Task<Guid> CreateAsync(CategoryPostDto dto); // DTO daxilində CompanyId olmalıdır
    Task UpdateAsync(CategoryPutDto dto); // DTO daxilində CompanyId olmalıdır
    Task UpdateOrdersAsync(Guid companyId, List<CategoryOrderUpdateDto> dtos);
    Task DeleteAsync(Guid id, Guid companyId);
}