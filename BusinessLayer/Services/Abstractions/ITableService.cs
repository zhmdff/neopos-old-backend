using BusinessLayer.DTOs.Table;

public interface ITableService
{
    Task<IEnumerable<TableGetDto>> GetAllAsync(Guid companyId);
    Task<TableGetDto> GetByIdAsync(Guid id, Guid companyId);
    Task CreateAsync(TablePostDto dto);
    Task UpdateAsync(TablePutDto dto);
    Task DeleteAsync(Guid id, Guid companyId);
    Task<IEnumerable<TableGetDto>> GetByHallIdAsync(Guid hallId, Guid companyId);
    Task UpdateOrdersAsync(Guid companyId, List<TableOrderUpdateDto> dtos);
}