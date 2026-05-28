using AutoMapper;
using BusinessLayer.DTOs.Supplier;
using BusinessLayer.Services.Abstractions;
using DAL.Server.Context;
using Domain.Common.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessLayer.Services.Implementations;

public class SupplierService : ISupplierService
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;

    public SupplierService(AppDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<IEnumerable<SupplierGetDto>> GetAllByCompanyIdAsync(Guid companyId)
    {
        var suppliers = await _context.Suppliers
            .Where(x => x.CompanyId == companyId && !x.IsDeleted)
            .AsNoTracking()
            .ToListAsync();

        return _mapper.Map<IEnumerable<SupplierGetDto>>(suppliers);
    }

    public async Task<SupplierGetDto> GetByIdAsync(Guid id)
    {
        var supplier = await _context.Suppliers
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

        if (supplier == null) throw new Exception("Tədarükçü tapılmadı!");

        return _mapper.Map<SupplierGetDto>(supplier);
    }

    public async Task CreateAsync(SupplierPostDto dto)
    {
        bool exists = await _context.Suppliers.AnyAsync(s =>
            s.CompanyId == dto.CompanyId &&
            s.Name.ToLower() == dto.Name.ToLower() &&
            !s.IsDeleted);

        if (exists) throw new Exception($"'{dto.Name}' adlı tədarükçü artıq mövcuddur!");

        var supplier = _mapper.Map<Supplier>(dto);

        supplier.CreatedAt = DateTime.UtcNow;
        supplier.CreatedBy = "System";
        supplier.CompanyId = dto.CompanyId; // DTO-dan gələn ID

        await _context.Suppliers.AddAsync(supplier);
        await _context.SaveChangesAsync();
    }

    public async Task<bool> UpdateAsync(Guid id, SupplierPostDto dto)
    {
        var supplier = await _context.Suppliers.FindAsync(id);
        if (supplier == null) throw new Exception("Tədarükçü tapılmadı!");

        if (supplier.Name.ToLower() != dto.Name.ToLower())
        {
            bool exists = await _context.Suppliers
                .AnyAsync(x => x.Name.ToLower() == dto.Name.ToLower()
                          && x.CompanyId == supplier.CompanyId
                          && !x.IsDeleted
                          && x.Id != id);

            if (exists) throw new Exception("Bu ad artıq başqa bir tədarükçü tərəfindən istifadə edilir!");
        }

        _mapper.Map(dto, supplier);
        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var supplier = await _context.Suppliers.FindAsync(id);
        if (supplier == null) throw new Exception("Tədarükçü tapılmadı!");

        supplier.IsDeleted = true;
        return await _context.SaveChangesAsync() > 0;
    }
}