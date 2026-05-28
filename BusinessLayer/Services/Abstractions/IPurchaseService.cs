using BusinessLayer.DTOs.Purchase;

namespace BusinessLayer.Services.Abstractions;

public interface IPurchaseService
{
    Task CreateAsync(PurchasePostDto dto);
    Task<(IEnumerable<PurchaseGetDto> items, int totalCount)> GetAllByCompanyIdAsync(Guid companyId, int pageNumber, int pageSize);
    Task<PurchaseGetDto> GetByIdAsync(Guid id);
}