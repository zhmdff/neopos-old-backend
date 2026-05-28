using BusinessLayer.DTOs.Workshop;

namespace BusinessLayer.Services.Abstractions;

public interface IWorkshopService
{
    // companyId parametrini əlavə etdik
    Task<IEnumerable<WorkshopGetDto>> GetAllAsync(Guid companyId);
    Task CreateAsync(WorkshopPostDto dto); // DTO daxilində CompanyId gəlir
    Task UpdateAsync(WorkshopPutDto dto); // DTO daxilində CompanyId gəlir
    Task DeleteAsync(Guid id, Guid companyId);
}