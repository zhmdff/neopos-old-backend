using BusinessLayer.DTOs.Hall;

namespace BusinessLayer.Services.Abstractions;

public interface IHallService
{
    Task<IEnumerable<HallGetDto>> GetAllAsync(Guid companyId);

    Task CreateAsync(HallPostDto dto);

    Task UpdateAsync(HallPutDto dto);

    Task UpdateOrdersAsync(Guid companyId, List<HallOrderUpdateDto> dtos);

    Task DeleteAsync(Guid id, Guid companyId);
}