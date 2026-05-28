using BusinessLayer.DTOs.Company;
using BusinessLayer.Services.Abstractions;
using DAL.Server.Context;
using Domain.Common.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessLayer.Services.Implementations;

public class CompanyPaymentMethodService : ICompanyPaymentMethodService
{
    private readonly AppDbContext _context;

    public CompanyPaymentMethodService(AppDbContext context) => _context = context;

    public async Task<List<CompanyPaymentMethodDto>> ListAsync(Guid companyId)
    {
        return await _context.CompanyPaymentMethods
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId && !x.IsDeleted)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.NameAz)
            .Select(x => new CompanyPaymentMethodDto
            {
                Id = x.Id,
                CompanyId = x.CompanyId,
                NameAz = x.NameAz,
                SortOrder = x.SortOrder,
            })
            .ToListAsync();
    }

    public async Task<CompanyPaymentMethodDto> AddAsync(Guid companyId, CompanyPaymentMethodPostDto dto, string createdBy)
    {
        var name = (dto.NameAz ?? "").Trim();
        if (string.IsNullOrEmpty(name)) throw new Exception("Ad boş ola bilməz.");
        if (name.Length > 120) throw new Exception("Ad çox uzundur (max 120).");

        var row = new CompanyPaymentMethod
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            NameAz = name,
            SortOrder = dto.SortOrder,
            CreatedBy = createdBy,
        };
        _context.CompanyPaymentMethods.Add(row);
        await _context.SaveChangesAsync();

        return new CompanyPaymentMethodDto
        {
            Id = row.Id,
            CompanyId = row.CompanyId,
            NameAz = row.NameAz,
            SortOrder = row.SortOrder,
        };
    }

    public async Task<bool> UpdateAsync(Guid companyId, Guid id, CompanyPaymentMethodPutDto dto)
    {
        var row = await _context.CompanyPaymentMethods
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId && !x.IsDeleted);
        if (row == null) return false;

        var name = (dto.NameAz ?? "").Trim();
        if (string.IsNullOrEmpty(name)) throw new Exception("Ad boş ola bilməz.");
        if (name.Length > 120) throw new Exception("Ad çox uzundur (max 120).");

        row.NameAz = name;
        row.SortOrder = dto.SortOrder;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(Guid companyId, Guid id)
    {
        var row = await _context.CompanyPaymentMethods
            .FirstOrDefaultAsync(x => x.Id == id && x.CompanyId == companyId && !x.IsDeleted);
        if (row == null) return false;

        row.IsDeleted = true;
        row.DeletedAt = DateTime.UtcNow;

        await _context.OrderHeaders
            .Where(o => o.CustomPaymentMethodId == id && o.CompanyId == companyId)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.CustomPaymentMethodId, (Guid?)null));

        await _context.SaveChangesAsync();
        return true;
    }
}
