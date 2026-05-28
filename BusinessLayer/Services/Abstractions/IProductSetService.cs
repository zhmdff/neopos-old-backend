using BusinessLayer.DTOs.ProductSet;

namespace BusinessLayer.Services.Abstractions;

public interface IProductSetService
{
    Task<ProductSetGetDto> CreateSetAsync(ProductSetPostDto dto);
    Task<List<ProductSetGetDto>> GetAllSetsAsync(Guid companyId, int skip, int take, string? search, Guid? categoryId, Guid? workshopId);
    Task<ProductSetGetDto> GetSetByIdAsync(Guid id, Guid companyId);
    Task DeleteSetAsync(Guid id, Guid companyId);
}