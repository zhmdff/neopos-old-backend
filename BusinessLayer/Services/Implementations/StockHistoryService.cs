using AutoMapper;
using BusinessLayer.DTOs.ProductStockHistory;
using BusinessLayer.Services.Abstractions;
using DAL.Server.Context;
using Microsoft.EntityFrameworkCore;

namespace BusinessLayer.Services.Implementations;

public class StockHistoryService : IStockHistoryService
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;

    public StockHistoryService(AppDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<(IEnumerable<StockHistoryGetDto> items, int totalCount)> GetAllByCompanyIdAsync(Guid companyId, int pageNumber, int pageSize)
    {
        var query = _context.ProductStockHistories
            .Include(h => h.Product)
            .Include(h => h.Warehouse)
            .Include(h => h.Supplier)
            .Where(x => x.CompanyId == companyId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt); // Ən son hərəkət ən üstdə

        // Cəmi sayı götürürük
        int totalCount = await query.CountAsync();

        // Səhifələmə tətbiq olunur
        var histories = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var dtos = _mapper.Map<IEnumerable<StockHistoryGetDto>>(histories);

        return (dtos, totalCount);
    }
}