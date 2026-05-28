using BusinessLayer.DTOs.Customer;

namespace BusinessLayer.Services.Abstractions;

public interface ICustomerService
{
    Task<List<CustomerGetDto>> SearchAsync(Guid companyId, string? q, int take = 40, int skip = 0);
    Task<CustomerGetDto> CreateAsync(CustomerPostDto dto, Guid companyId);
    Task<CustomerGetDto> UpdateAsync(Guid id, CustomerPostDto dto, Guid companyId);
}
