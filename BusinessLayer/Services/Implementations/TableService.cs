using AutoMapper;
using BusinessLayer.DTOs.Table;
using BusinessLayer.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using DAL.Server.Context;
using Application.Interfaces;
using Domain.Common.Entities;
using BusinessLayer.DTOs.OrderHeader;
using Domain.Enums;

namespace BusinessLayer.Services.Implementations;

public class TableService : ITableService
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;
    private readonly ITranslationService _translationService;

    public TableService(AppDbContext context, IMapper mapper, ITranslationService translationService)
    {
        _context = context;
        _mapper = mapper;
        _translationService = translationService;
    }

    public async Task<IEnumerable<TableGetDto>> GetAllAsync(Guid companyId)
    {
        var tables = await _context.Tables
            .Include(t => t.Hall)
            .Where(t => t.CompanyId == companyId && !t.IsDeleted) // Şirkət filtri
            .OrderBy(t => t.OrderIndex)
            .ToListAsync();

        return _mapper.Map<IEnumerable<TableGetDto>>(tables);
    }

    public async Task CreateAsync(TablePostDto dto)
    {
        // Zalın bu şirkətə aid olub-olmadığını yoxlayırıq
        var hall = await _context.Halls
            .FirstOrDefaultAsync(h => h.Id == dto.HallId && h.CompanyId == dto.CompanyId && !h.IsDeleted);
        if (hall == null) throw new Exception("Seçilmiş Zal tapılmadı!");
        if (hall.IsTableHourActive && (dto.TableHourLimitMinutes is not > 0))
            throw new Exception("Masa saat limitini daxil edin (məs. 3:00 və ya 1:30).");

        bool exists = await _context.Tables.AnyAsync(t =>
            t.CompanyId == dto.CompanyId &&
            t.HallId == dto.HallId &&
            t.NameAz.ToLower() == dto.NameAz.ToLower() &&
            !t.IsDeleted);

        if (exists) throw new Exception($"Bu zalda '{dto.NameAz}' adlı masa artıq mövcuddur!");

        var table = _mapper.Map<Table>(dto);
        var translations = await _translationService.TranslateTextAsync(dto.NameAz, new List<string> { "en", "ru" });
        table.NameEn = translations.GetValueOrDefault("en", dto.NameAz);
        table.NameRu = translations.GetValueOrDefault("ru", dto.NameAz);

        if (dto.OrderIndex is > 0)
        {
            table.OrderIndex = dto.OrderIndex.Value;
        }
        else
        {
            int maxIndex = await _context.Tables
                .Where(t => t.CompanyId == dto.CompanyId && t.HallId == dto.HallId && !t.IsDeleted)
                .Select(t => (int?)t.OrderIndex)
                .MaxAsync() ?? 0;

            table.OrderIndex = maxIndex + 1;
        }
        table.CreatedAt = DateTime.UtcNow;
        table.CreatedBy = "Admin";
        table.CompanyId = dto.CompanyId;

        await _context.Tables.AddAsync(table);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(TablePutDto dto)
    {
        // 1. Masanı bazadan tapırıq
        var table = await _context.Tables
            .FirstOrDefaultAsync(t => t.Id == dto.Id && t.CompanyId == dto.CompanyId && !t.IsDeleted);

        if (table == null) throw new Exception("Masa tapılmadı!");

        var hall = await _context.Halls
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.Id == dto.HallId && h.CompanyId == dto.CompanyId && !h.IsDeleted);
        if (hall?.IsTableHourActive == true && (dto.TableHourLimitMinutes is not > 0))
            throw new Exception("Masa saat limitini daxil edin (məs. 3:00 və ya 1:30).");

        // 2. Adın unikal olub-olmadığını yoxlayırıq (Eyni zal daxilində)
        bool nameExists = await _context.Tables.AnyAsync(t =>
            t.CompanyId == dto.CompanyId &&
            t.HallId == dto.HallId &&
            t.NameAz.ToLower() == dto.NameAz.ToLower() &&
            t.Id != dto.Id &&
            !t.IsDeleted);

        if (nameExists) throw new Exception("Bu zalda bu adda başqa bir masa artıq mövcuddur!");

        // 3. Orijinal dəyərləri (qorunmalı olanları) saxlayırıq
        var currentOrderIndex = table.OrderIndex;
        var currentHallId = table.HallId;

        // 4. Mapper-i işə salırıq (Bütün sahələri güncəlləyirik)
        _mapper.Map(dto, table);

        // 5. Tərcümə məntiqi (Ad dəyişibsə yenidən tərcümə et)
        if (table.NameAz != dto.NameAz)
        {
            var translations = await _translationService.TranslateTextAsync(dto.NameAz, new List<string> { "en", "ru" });
            table.NameEn = translations.GetValueOrDefault("en", dto.NameAz);
            table.NameRu = translations.GetValueOrDefault("ru", dto.NameAz);
        }

        // 6. Qorunmalı olan dəyərləri və sığortaları bərpa edirik
        table.OrderIndex = currentOrderIndex;

        // Əgər DTO-da HallId səhvən boş gəlibsə, köhnəni saxlayırıq (Xəta verməməsi üçün)
        if (dto.HallId == Guid.Empty)
        {
            table.HallId = currentHallId;
        }

        table.LastModifiedAt = DateTime.UtcNow;

        // 7. Bazada yeniləyirik
        _context.Tables.Update(table);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateOrdersAsync(Guid companyId, List<TableOrderUpdateDto> dtos)
    {
        var ids = dtos.Select(x => x.Id).ToList();
        var tables = await _context.Tables.Where(t => ids.Contains(t.Id) && t.CompanyId == companyId).ToListAsync();

        foreach (var table in tables)
        {
            var dto = dtos.First(x => x.Id == table.Id);
            table.OrderIndex = dto.OrderIndex;
        }

        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id, Guid companyId)
    {
        var table = await _context.Tables.FirstOrDefaultAsync(t => t.Id == id && t.CompanyId == companyId);
        if (table == null) throw new Exception("Masa tapılmadı!");

        table.IsDeleted = true;
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<TableGetDto>> GetByHallIdAsync(Guid hallId, Guid companyId)
    {
        var tables = await _context.Tables
            .Include(t => t.Hall)
            .Where(t => t.HallId == hallId && t.CompanyId == companyId && !t.IsDeleted)
            .OrderBy(t => t.OrderIndex)
            .AsNoTracking()
            .ToListAsync();

        var tableIds = tables.Select(t => t.Id).ToList();

        // Aktiv sifarişləri də yalnız bu şirkət üçün çəkirik
        var activeOrders = await _context.OrderHeaders
            .Include(o => o.OrderDetails)
            .Include(o => o.CustomPaymentMethod)
            .Where(o => tableIds.Contains(o.TableId) && !o.IsClosed && o.CompanyId == companyId)
            .AsNoTracking()
            .ToListAsync();

        var tableDtos = _mapper.Map<List<TableGetDto>>(tables);

        foreach (var dto in tableDtos)
        {
            var order = activeOrders.FirstOrDefault(o => o.TableId == dto.Id);
            if (order != null)
            {
                dto.ActiveOrder = _mapper.Map<OrderHeaderGetDto>(order);
                dto.Status = (int)TableStatus.Occupied;
            }
            else
            {
                dto.ActiveOrder = null;
                dto.Status = (int)TableStatus.Empty;
            }
        }
        return tableDtos;
    }

    public async Task<TableGetDto> GetByIdAsync(Guid id, Guid companyId)
    {
        var table = await _context.Tables
            .Include(t => t.Hall)
            .FirstOrDefaultAsync(t => t.Id == id && t.CompanyId == companyId && !t.IsDeleted);

        return table == null ? null : _mapper.Map<TableGetDto>(table);
    }
}