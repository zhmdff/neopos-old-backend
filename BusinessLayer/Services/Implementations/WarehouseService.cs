using Application.Interfaces;
using AutoMapper;
using BusinessLayer.DTOs.Warehouse;
using BusinessLayer.Services.Abstractions;
using DAL.Server.Context;
using Domain.Common.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessLayer.Services.Implementations;

public class WarehouseService : IWarehouseService
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;

    public WarehouseService(AppDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<IEnumerable<WarehouseGetDto>> GetAllByCompanyIdAsync(Guid companyId)
    {
        var warehouses = await _context.Warehouses
            .Where(x => x.CompanyId == companyId && !x.IsDeleted)
            .AsNoTracking()
            .ToListAsync();

        return _mapper.Map<IEnumerable<WarehouseGetDto>>(warehouses);
    }

    public async Task<WarehouseGetDto> GetByIdAsync(Guid id)
    {
        var warehouse = await _context.Warehouses
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

        if (warehouse == null) throw new Exception("Anbar tapılmadı!");

        return _mapper.Map<WarehouseGetDto>(warehouse);
    }

    public async Task CreateAsync(WarehousePostDto dto)
    {
        // Eyni şirkət daxilində eyni adlı anbar yoxlanışı
        bool exists = await _context.Warehouses.AnyAsync(w =>
            w.CompanyId == dto.CompanyId &&
            w.Name.ToLower() == dto.Name.ToLower() &&
            !w.IsDeleted);

        if (exists) throw new Exception($"'{dto.Name}' adlı anbar artıq mövcuddur!");

        var warehouse = _mapper.Map<Warehouse>(dto);

        // Audit və Şirkət mənimsətmələri (Sənin standartın)
        warehouse.CreatedAt = DateTime.UtcNow;
        warehouse.CreatedBy = "System"; // Gələcəkdə dinamik olar
        warehouse.CompanyId = dto.CompanyId;

        await _context.Warehouses.AddAsync(warehouse);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Guid id, WarehousePostDto dto)
    {
        var warehouse = await _context.Warehouses.FindAsync(id);
        if (warehouse == null) throw new Exception("Anbar tapılmadı!");

        _mapper.Map(dto, warehouse);
        warehouse.LastModifiedAt = DateTime.UtcNow; // Update tarixini də yeniləyək

        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var warehouse = await _context.Warehouses.FindAsync(id);
        if (warehouse == null) throw new Exception("Anbar tapılmadı!");

        warehouse.IsDeleted = true;
        await _context.SaveChangesAsync();
    }

    public async Task SetDefaultSaleWarehouseAsync(Guid id, Guid companyId)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var allWarehouses = await _context.Warehouses
                .Where(w => w.CompanyId == companyId && !w.IsDeleted)
                .ToListAsync();

            foreach (var w in allWarehouses)
            {
                w.IsDefaultSale = false;
            }

            var target = allWarehouses.FirstOrDefault(x => x.Id == id);
            if (target == null) throw new Exception("Anbar tapılmadı!");

            target.IsDefaultSale = true;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}