using BusinessLayer.DTOs.HallTimeDiscount;
using BusinessLayer.Services.Abstractions;
using BusinessLayer.Utilities;
using DAL.Server.Context;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessLayer.Services.Implementations;

public class HallTimeDiscountRuleService : IHallTimeDiscountRuleService
{
    private readonly AppDbContext _context;

    public HallTimeDiscountRuleService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<HallTimeDiscountRuleGetDto>> GetByHallAsync(Guid hallId, Guid companyId)
    {
        var list = await _context.Set<HallTimeDiscountRule>()
            .AsNoTracking()
            .Where(r => r.HallId == hallId && r.CompanyId == companyId && !r.IsDeleted)
            .OrderBy(r => r.StartTime)
            .ToListAsync();

        return list.Select(MapToGet).ToList();
    }

    public async Task<HallTimeDiscountRuleGetDto> CreateAsync(HallTimeDiscountRulePostDto dto)
    {
        await EnsureHallAsync(dto.HallId, dto.CompanyId);
        ValidateDto(dto.IsPercentageDiscount, dto.DiscountPercentage, dto.DiscountAmount, dto.StartTime, dto.EndTime);

        var entity = new HallTimeDiscountRule
        {
            Id = Guid.NewGuid(),
            CompanyId = dto.CompanyId,
            HallId = dto.HallId,
            StartTime = ParseTime(dto.StartTime),
            EndTime = ParseTime(dto.EndTime),
            IsPercentageDiscount = dto.IsPercentageDiscount,
            DiscountPercentage = dto.DiscountPercentage,
            DiscountAmount = dto.DiscountAmount,
            IsEnabled = dto.IsEnabled,
            Label = string.IsNullOrWhiteSpace(dto.Label) ? null : dto.Label.Trim(),
            CreatedBy = "Boss",
        };

        await _context.Set<HallTimeDiscountRule>().AddAsync(entity);
        await _context.SaveChangesAsync();
        return MapToGet(entity);
    }

    public async Task UpdateAsync(HallTimeDiscountRulePutDto dto)
    {
        var entity = await _context.Set<HallTimeDiscountRule>()
            .FirstOrDefaultAsync(r => r.Id == dto.Id && r.CompanyId == dto.CompanyId && !r.IsDeleted)
            ?? throw new Exception("Qayda tapılmadı!");

        ValidateDto(dto.IsPercentageDiscount, dto.DiscountPercentage, dto.DiscountAmount, dto.StartTime, dto.EndTime);

        entity.StartTime = ParseTime(dto.StartTime);
        entity.EndTime = ParseTime(dto.EndTime);
        entity.IsPercentageDiscount = dto.IsPercentageDiscount;
        entity.DiscountPercentage = dto.DiscountPercentage;
        entity.DiscountAmount = dto.DiscountAmount;
        entity.IsEnabled = dto.IsEnabled;
        entity.Label = string.IsNullOrWhiteSpace(dto.Label) ? null : dto.Label.Trim();
        entity.HallId = dto.HallId;

        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id, Guid companyId)
    {
        var entity = await _context.Set<HallTimeDiscountRule>()
            .FirstOrDefaultAsync(r => r.Id == id && r.CompanyId == companyId && !r.IsDeleted)
            ?? throw new Exception("Qayda tapılmadı!");

        entity.IsDeleted = true;
        await _context.SaveChangesAsync();
    }

    public async Task<HallTimeDiscountRule?> ResolveActiveForOpenOrderAsync(Guid hallId, Guid companyId, DateTime localDateTime)
    {
        var rules = await _context.Set<HallTimeDiscountRule>()
            .AsNoTracking()
            .Where(r => r.HallId == hallId && r.CompanyId == companyId && !r.IsDeleted && r.IsEnabled)
            .ToListAsync();

        return HallTimeDiscountHelper.PickActiveRule(rules, localDateTime);
    }

    private async Task EnsureHallAsync(Guid hallId, Guid companyId)
    {
        var ok = await _context.Halls.AnyAsync(h => h.Id == hallId && h.CompanyId == companyId && !h.IsDeleted);
        if (!ok) throw new Exception("Zal tapılmadı!");
    }

    private static void ValidateDto(bool isPct, decimal pct, decimal amt, string start, string end)
    {
        _ = ParseTime(start);
        _ = ParseTime(end);
        if (isPct)
        {
            if (pct <= 0 || pct > 100) throw new Exception("Faiz endirim 0–100 aralığında olmalıdır.");
        }
        else if (amt <= 0)
        {
            throw new Exception("Məbləğ endirimi 0-dan böyük olmalıdır.");
        }
    }

    private static TimeSpan ParseTime(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new Exception("Vaxt daxil edin (24 saat, məs. 18:00).");
        var s = value.Trim();
        if (TimeSpan.TryParse(s, out var ts)) return ts;
        if (s.Length >= 5 && TimeSpan.TryParse(s + ":00", out ts)) return ts;
        throw new Exception($"Yanlış vaxt formatı: {value}");
    }

    private static string FormatTime(TimeSpan t) => t.ToString(@"hh\:mm");

    private static HallTimeDiscountRuleGetDto MapToGet(HallTimeDiscountRule r) => new()
    {
        Id = r.Id,
        HallId = r.HallId,
        StartTime = FormatTime(r.StartTime),
        EndTime = FormatTime(r.EndTime),
        IsPercentageDiscount = r.IsPercentageDiscount,
        DiscountPercentage = r.DiscountPercentage,
        DiscountAmount = r.DiscountAmount,
        IsEnabled = r.IsEnabled,
        Label = r.Label,
    };
}
