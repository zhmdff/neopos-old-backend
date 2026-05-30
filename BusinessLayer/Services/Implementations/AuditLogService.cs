using AutoMapper;
using BusinessLayer.DTOs.Audit;
using BusinessLayer.DTOs.Product;
using BusinessLayer.Services.Abstractions;
using BusinessLayer.Utilities;
using DAL.Server.Context;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessLayer.Services.Implementations;

public class AuditLogService : IAuditLogService
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;
    private readonly IBossLiveNotifyDispatcher _bossLiveNotify;
    private readonly IBossMasterNotifyRelayService _bossMasterNotifyRelay;
    private readonly ILogger<AuditLogService> _logger;
    private DateTime AzTime => DateTime.SpecifyKind(DateTime.UtcNow.AddHours(4), DateTimeKind.Unspecified);

    public AuditLogService(
        AppDbContext context,
        IMapper mapper,
        IBossLiveNotifyDispatcher bossLiveNotify,
        IBossMasterNotifyRelayService bossMasterNotifyRelay,
        ILogger<AuditLogService> logger)
    {
        _context = context;
        _mapper = mapper;
        _bossLiveNotify = bossLiveNotify;
        _bossMasterNotifyRelay = bossMasterNotifyRelay;
        _logger = logger;
    }

    public async Task LogActionAsync(AuditLogPostDto dto)
    {
        var log = _mapper.Map<AuditLog>(dto);
        log.CreatedAt = AzTime;

        if (string.IsNullOrWhiteSpace(log.UserName))
            log.UserName = string.IsNullOrWhiteSpace(dto.UserName) ? "—" : dto.UserName.Trim();
        if (string.IsNullOrWhiteSpace(log.CreatedBy))
            log.CreatedBy = string.IsNullOrWhiteSpace(dto.CreatedBy) ? log.UserName : dto.CreatedBy.Trim();

        await _context.AuditLogs.AddAsync(log);
        await _context.SaveChangesAsync();

        try
        {
            await _bossLiveNotify.DispatchAuditAsync(dto.CompanyId, dto);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Boss canlı bildiriş xətası");
        }

        try
        {
            await _bossMasterNotifyRelay.TryRelayAuditAsync(dto);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Boss master relay xətası");
        }
    }

    public async Task<List<AuditLogGetDto>> GetCompanyLogsAsync(
        Guid companyId,
        int take = 50,
        DateTime? fromInclusive = null,
        DateTime? toInclusive = null)
    {
        take = Math.Clamp(take, 1, 5000);
        var q = _context.AuditLogs.AsNoTracking().Where(l => l.CompanyId == companyId);

        if (fromInclusive.HasValue)
        {
            var f = ReportQueryBakuTime.ToBakuWallForDbComparison(fromInclusive.Value);
            q = q.Where(l => l.CreatedAt >= f);
        }

        if (toInclusive.HasValue)
        {
            var t = ReportQueryBakuTime.ToBakuWallForDbComparison(toInclusive.Value);
            q = q.Where(l => l.CreatedAt <= t);
        }

        var logs = await q
            .OrderByDescending(l => l.CreatedAt)
            .Take(take)
            .ToListAsync();

        return _mapper.Map<List<AuditLogGetDto>>(logs);
    }

    public async Task<List<AuditLogGetDto>> GetShiftLogsAsync(Guid shiftId, Guid companyId, int take = 50)
    {
        var shift = await _context.CashShifts
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == shiftId && s.CompanyId == companyId);

        if (shift == null)
            throw new Exception("Növbə tapılmadı!");

        var start = shift.StartTime;
        var end = shift.EndTime ?? AzTime;

        var logs = await _context.AuditLogs
            .AsNoTracking()
            .Where(l => l.CompanyId == companyId)
            .Where(l => l.CreatedAt >= start && l.CreatedAt <= end)
            .OrderByDescending(l => l.CreatedAt)
            .Take(take)
            .ToListAsync();

        return _mapper.Map<List<AuditLogGetDto>>(logs);
    }

    public async Task<List<OrderLineDeletionItemDto>> GetProductDeletionLogsInRangeAsync(
        DateTime start,
        DateTime end,
        Guid companyId)
    {
        start = ReportQueryBakuTime.ToBakuWallForDbComparison(start);
        end = ReportQueryBakuTime.ToBakuWallForDbComparison(end);

        return await _context.AuditLogs
            .AsNoTracking()
            .Where(l => l.CompanyId == companyId)
            .Where(l => l.Action != null && l.Action.Contains("MƏHSUL SİLİNDİ"))
            .Where(l => l.CreatedAt >= start && l.CreatedAt <= end)
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new OrderLineDeletionItemDto
            {
                Id = l.Id,
                CreatedAt = l.CreatedAt,
                UserName = l.UserName,
                TableName = l.TableName,
                HallName = l.HallName,
                Description = l.Description,
                LineProductName = l.LineProductName,
                LineQuantity = l.LineQuantity,
                LineUnitPrice = l.LineUnitPrice,
                LineTotal = l.LineTotal
            })
            .ToListAsync();
    }
}
