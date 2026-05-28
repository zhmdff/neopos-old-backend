using BusinessLayer.DTOs.Warehouse;

namespace BusinessLayer.Services.Abstractions;

public interface IWarehouseService
{
    Task<IEnumerable<WarehouseGetDto>> GetAllByCompanyIdAsync(Guid companyId);
    Task<WarehouseGetDto> GetByIdAsync(Guid id);
    Task CreateAsync(WarehousePostDto dto);
    Task UpdateAsync(Guid id, WarehousePostDto dto);
    Task DeleteAsync(Guid id);
    Task SetDefaultSaleWarehouseAsync(Guid id, Guid companyId);
}