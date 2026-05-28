using AutoMapper;
using BusinessLayer.DTOs.Customer;
using BusinessLayer.Services.Abstractions;
using DAL.Server.Context;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessLayer.Services.Implementations;

public class CustomerService : ICustomerService
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;

    public CustomerService(AppDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<List<CustomerGetDto>> SearchAsync(Guid companyId, string? q, int take = 40, int skip = 0)
    {
        take = Math.Clamp(take, 1, 100);
        skip = Math.Max(0, skip);

        var query = _context.Customers.AsNoTracking()
            .Where(c => c.CompanyId == companyId && !c.IsDeleted);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var t = q.Trim();
            var lower = t.ToLower();
            query = query.Where(c =>
                c.FullName.ToLower().Contains(lower) ||
                c.Phone.Contains(t));
        }

        var list = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        return _mapper.Map<List<CustomerGetDto>>(list);
    }

    public async Task<CustomerGetDto> CreateAsync(CustomerPostDto dto, Guid companyId)
    {
        if (string.IsNullOrWhiteSpace(dto.FullName))
            throw new Exception("Ad, soyad daxil edin.");
        if (string.IsNullOrWhiteSpace(dto.Phone))
            throw new Exception("Telefon nömrəsi daxil edin.");

        var c = new Customer
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            FullName = dto.FullName.Trim(),
            Phone = dto.Phone.Trim(),
            Address = string.IsNullOrWhiteSpace(dto.Address) ? null : dto.Address.Trim(),
            BirthDay = dto.BirthDay,
            CreatedBy = "Terminal",
        };

        await _context.Customers.AddAsync(c);
        await _context.SaveChangesAsync();

        return _mapper.Map<CustomerGetDto>(c);
    }

    public async Task<CustomerGetDto> UpdateAsync(Guid id, CustomerPostDto dto, Guid companyId)
    {
        if (string.IsNullOrWhiteSpace(dto.FullName))
            throw new Exception("Ad, soyad daxil edin.");
        if (string.IsNullOrWhiteSpace(dto.Phone))
            throw new Exception("Telefon nömrəsi daxil edin.");

        var c = await _context.Customers.FirstOrDefaultAsync(x =>
            x.Id == id && x.CompanyId == companyId && !x.IsDeleted);
        if (c == null)
            throw new Exception("Müştəri tapılmadı.");

        c.FullName = dto.FullName.Trim();
        c.Phone = dto.Phone.Trim();
        c.Address = string.IsNullOrWhiteSpace(dto.Address) ? null : dto.Address.Trim();
        c.BirthDay = dto.BirthDay;

        await _context.SaveChangesAsync();

        return _mapper.Map<CustomerGetDto>(c);
    }
}
