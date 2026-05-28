using BusinessLayer.DTOs.ProductStockHistory;

namespace BusinessLayer.Services.Abstractions;

public interface IStockHistoryService
{
    // Tuple vasitəsilə həm siyahını, həm də cəmi sayı qaytarırıq
    Task<(IEnumerable<StockHistoryGetDto> items, int totalCount)> GetAllByCompanyIdAsync(Guid companyId, int pageNumber, int pageSize);
}