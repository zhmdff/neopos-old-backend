using BusinessLayer.DTOs.User;

namespace BusinessLayer.Services.Abstractions;

public interface IUserService
{
    // Bütün metodlara companyId təhlükəsizlik parametri əlavə edildi
    Task<IEnumerable<UserGetDto>> GetAllAsync(Guid companyId);
    Task<UserGetDto> GetByIdAsync(Guid id, Guid companyId, Guid? viewerUserId = null);
    Task CreateAsync(UserPostDto dto); // DTO daxilində CompanyId var
    Task UpdateAsync(UserPutDto dto); // DTO daxilində CompanyId var
    Task DeleteAsync(Guid id, Guid companyId);
}