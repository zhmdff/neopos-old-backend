using BusinessLayer.DTOs.HallTimeDiscount;
using Domain.Entities;

namespace BusinessLayer.Services.Abstractions;

public interface IHallTimeDiscountRuleService
{
    Task<IReadOnlyList<HallTimeDiscountRuleGetDto>> GetByHallAsync(Guid hallId, Guid companyId);
    Task<HallTimeDiscountRuleGetDto> CreateAsync(HallTimeDiscountRulePostDto dto);
    Task UpdateAsync(HallTimeDiscountRulePutDto dto);
    Task DeleteAsync(Guid id, Guid companyId);
    Task<HallTimeDiscountRule?> ResolveActiveForOpenOrderAsync(Guid hallId, Guid companyId, DateTime localDateTime);
}
