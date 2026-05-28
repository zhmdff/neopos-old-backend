using BusinessLayer.DTOs.Company;

namespace BusinessLayer.Services.Abstractions;

public interface ICompanyPaymentMethodService
{
    Task<List<CompanyPaymentMethodDto>> ListAsync(Guid companyId);
    Task<CompanyPaymentMethodDto> AddAsync(Guid companyId, CompanyPaymentMethodPostDto dto, string createdBy);
    Task<bool> UpdateAsync(Guid companyId, Guid id, CompanyPaymentMethodPutDto dto);
    Task<bool> DeleteAsync(Guid companyId, Guid id);
}
