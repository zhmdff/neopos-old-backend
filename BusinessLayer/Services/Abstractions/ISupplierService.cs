using BusinessLayer.DTOs.Supplier;

namespace BusinessLayer.Services.Abstractions;

public interface ISupplierService
{
    Task<IEnumerable<SupplierGetDto>> GetAllByCompanyIdAsync(Guid companyId);
    Task<SupplierGetDto> GetByIdAsync(Guid id);
    Task CreateAsync(SupplierPostDto dto);
    Task<bool> UpdateAsync(Guid id, SupplierPostDto dto);
    Task<bool> DeleteAsync(Guid id);
}