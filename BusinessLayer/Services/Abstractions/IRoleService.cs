using BusinessLayer.DTOs.Role;

namespace BusinessLayer.Services.Abstractions;

public interface IRoleService
{
    // Bütün metodlara companyId təhlükəsizlik parametri əlavə edildi
    Task<IEnumerable<RoleGetDto>> GetAllAsync(Guid companyId);
    Task<RoleGetDto> GetByIdAsync(Guid id, Guid companyId);
    Task CreateAsync(RolePostDto dto); // DTO daxilində CompanyId var
    Task DeleteAsync(Guid id, Guid companyId);
    Task UpdateAsync(RolePutDto dto); // DTO daxilində CompanyId var
}