using Application.Interfaces;
using AutoMapper;
using BusinessLayer.DTOs.Hall;
using BusinessLayer.Services.Abstractions;
using DAL.Server.Context;
using Domain.Common.Entities;
using Microsoft.EntityFrameworkCore;

public class HallService : IHallService
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;
    private readonly ITranslationService _translationService;

    public HallService(AppDbContext context, IMapper mapper, ITranslationService translationService)
    {
        _context = context;
        _mapper = mapper;
        _translationService = translationService;
    }

    // 1. GET ALL - Şirkətə görə filtr mütləqdir
    public async Task<IEnumerable<HallGetDto>> GetAllAsync(Guid companyId)
    {
        var halls = await _context.Halls
            .Include(h => h.Tables
                .Where(t => !t.IsDeleted)
                .OrderBy(t => t.OrderIndex)) // 🔥 BURANI ƏLAVƏ ET: Masaları öz daxilində sırala
            .Where(h => h.CompanyId == companyId && !h.IsDeleted)
            .OrderBy(h => h.OrderIndex) // Zalları sırala
            .ToListAsync();

        return _mapper.Map<IEnumerable<HallGetDto>>(halls);
    }

    // 2. UPDATE ORDERS - Şirkət yoxlanışı
    public async Task UpdateOrdersAsync(Guid companyId, List<HallOrderUpdateDto> dtos)
    {
        var ids = dtos.Select(x => x.Id).ToList();

        // Yalnız həmin şirkətə aid zallar
        var halls = await _context.Halls
            .Where(h => ids.Contains(h.Id) && h.CompanyId == companyId)
            .ToListAsync();

        foreach (var hall in halls)
        {
            var dto = dtos.First(x => x.Id == hall.Id);
            hall.OrderIndex = dto.OrderIndex;
        }

        await _context.SaveChangesAsync();
    }

    // 3. CREATE - Şirkətə özəl ad yoxlanışı
    public async Task CreateAsync(HallPostDto dto)
    {
        // Yalnız bu şirkətdə eyni adlı zal varmı? (Başqa restoranda eyni ad ola bilər, mane olmamalıyıq)
        bool exists = await _context.Halls.AnyAsync(h =>
            h.CompanyId == dto.CompanyId &&
            h.NameAz.ToLower() == dto.NameAz.ToLower() &&
            !h.IsDeleted);

        if (exists)
            throw new Exception($"'{dto.NameAz}' adlı zal bu restoranda artıq mövcuddur!");

        var hall = _mapper.Map<Hall>(dto);
        var translations = await _translationService.TranslateTextAsync(dto.NameAz, ["en", "ru"]);

        hall.NameEn = translations.GetValueOrDefault("en", dto.NameAz);
        hall.NameRu = translations.GetValueOrDefault("ru", dto.NameAz);

        int maxIndex = await _context.Halls
            .Where(h => h.CompanyId == dto.CompanyId && !h.IsDeleted)
            .Select(h => (int?)h.OrderIndex)
            .MaxAsync() ?? 0;

        hall.OrderIndex = maxIndex + 1;
        hall.CreatedAt = DateTime.UtcNow;
        hall.CreatedBy = "Admin";
        hall.CompanyId = dto.CompanyId; // Mütləq set edilir

        await _context.Halls.AddAsync(hall);
        await _context.SaveChangesAsync();
    }

    // 4. UPDATE - Təhlükəsizlik yoxlanışı
    public async Task UpdateAsync(HallPutDto dto)
    {
        // Həm ID, həm CompanyId yoxlayırıq ki, kimsə başqasının zalını editləməsin
        var hall = await _context.Halls
            .FirstOrDefaultAsync(h => h.Id == dto.Id && h.CompanyId == dto.CompanyId && !h.IsDeleted);

        if (hall == null) throw new Exception("Zal tapılmadı və ya giriş icazəniz yoxdur!");

        // Ad dəyişibsə, yeni adın bu şirkətdə olub-olmadığını yoxla
        bool nameExists = await _context.Halls.AnyAsync(h =>
            h.CompanyId == dto.CompanyId &&
            h.NameAz.ToLower() == dto.NameAz.ToLower() &&
            h.Id != dto.Id &&
            !h.IsDeleted);

        if (nameExists)
            throw new Exception($"'{dto.NameAz}' adı bu restoranda başqa bir zal üçün artıq istifadə olunub!");

        if (hall.NameAz != dto.NameAz)
        {
            var translations = await _translationService.TranslateTextAsync(dto.NameAz, ["en", "ru"]);
            hall.NameEn = translations.GetValueOrDefault("en", dto.NameAz);
            hall.NameRu = translations.GetValueOrDefault("ru", dto.NameAz);
        }

        var originalOrderIndex = hall.OrderIndex;
        _mapper.Map(dto, hall);
        hall.OrderIndex = originalOrderIndex;
        // Mapper konfiqurasiyası dəyişsə belə, bu flag həmişə düzgün yazılsın.
        hall.IsGuestCountEnabled = dto.IsGuestCountEnabled;
        hall.IsTableHourActive = dto.IsTableHourActive;
        hall.LastModifiedAt = DateTime.UtcNow;

        _context.Halls.Update(hall);
        await _context.SaveChangesAsync();
    }

    // 5. DELETE — zal və ona aid masalar verilənlər bazasından tam silinir (soft delete yox).
    public async Task DeleteAsync(Guid id, Guid companyId)
    {
        var hall = await _context.Halls
            .FirstOrDefaultAsync(h => h.Id == id && h.CompanyId == companyId);

        if (hall == null) throw new Exception("Zal tapılmadı!");

        var tableIds = await _context.Tables
            .Where(t => t.HallId == id && t.CompanyId == companyId)
            .Select(t => t.Id)
            .ToListAsync();

        if (tableIds.Count > 0)
        {
            var hasOrderHistory = await _context.OrderHeaders
                .AnyAsync(oh => oh.CompanyId == companyId && tableIds.Contains(oh.TableId));

            if (hasOrderHistory)
            {
                throw new Exception(
                    "Bu zala aid masalarda sifariş tarixçəsi var. Zal tam silinə bilməz.");
            }
        }

        await using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            var discountRules = await _context.HallTimeDiscountRules
                .Where(r => r.HallId == id && r.CompanyId == companyId)
                .ToListAsync();
            if (discountRules.Count > 0)
                _context.HallTimeDiscountRules.RemoveRange(discountRules);

            var tables = await _context.Tables
                .Where(t => t.HallId == id && t.CompanyId == companyId)
                .ToListAsync();
            if (tables.Count > 0)
                _context.Tables.RemoveRange(tables);

            _context.Halls.Remove(hall);
            await _context.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}