using AutoMapper;
using BusinessLayer.DTOs.Audit;
using BusinessLayer.DTOs.Product;
using BusinessLayer.Hubs;
using BusinessLayer.Utilities;
using BusinessLayer.Services.Abstractions;
using DAL.Server.Context;
using Domain.Entities;
using Microsoft.AspNetCore.SignalR; // 🔥 SignalR üçün mütləq lazımdır
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
namespace BusinessLayer.Services.Implementations;

public class AuditLogService : IAuditLogService
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;
    private readonly IHubContext<NotificationHub> _hubContext; // 🔥 HubContext əlavə edildi
    private readonly IBossWebPushService _bossWebPush;
    private readonly IBossTelegramNotifyService _bossTelegramNotify;
    private readonly ILogger<AuditLogService> _logger;
    private DateTime AzTime => DateTime.SpecifyKind(DateTime.UtcNow.AddHours(4), DateTimeKind.Unspecified);

    public AuditLogService(
        AppDbContext context,
        IMapper mapper,
        IHubContext<NotificationHub> hubContext,
        IBossWebPushService bossWebPush,
        IBossTelegramNotifyService bossTelegramNotify,
        ILogger<AuditLogService> logger)
    {
        _context = context;
        _mapper = mapper;
        _hubContext = hubContext; // 🔥 Dependency Injection
        _bossWebPush = bossWebPush;
        _bossTelegramNotify = bossTelegramNotify;
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
            var groupKey = dto.CompanyId.ToString("D").ToLowerInvariant();
            await _hubContext.Clients.Group(groupKey)
                .SendAsync("ReceiveNotification", new
                {
                    title = dto.Action.ToUpper(), // Məs: "MƏHSUL SİLİNDİ ❗"
                    body = TelegramAuditNotifyHelper.StripInternalTags(dto.Description),
                    tableName = dto.TableName,
                    hallName = dto.HallName,
                    userName = dto.UserName,
                    time = AzTime.ToString("HH:mm")
                });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR bildiriş xətası");
        }

        try
        {
            await _bossTelegramNotify.TryNotifyAuditAsync(
                dto.CompanyId,
                dto.Action ?? string.Empty,
                dto.Description,
                dto.UserName,
                dto.TableName,
                dto.HallName,
                AzTime.ToString("HH:mm"),
                AzTime);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Telegram audit bildiriş xətası");
        }

        try
        {
            var pushKind = TelegramAuditNotifyHelper.ClassifyKind(dto.Action, dto.Description);
            if (!string.IsNullOrEmpty(pushKind))
            {
                var prefsJson = await _context.Companies.AsNoTracking()
                    .Where(c => c.Id == dto.CompanyId)
                    .Select(c => c.TelegramNotifyPrefsJson)
                    .FirstOrDefaultAsync();
                if (TelegramAuditNotifyHelper.IsKindEnabled(pushKind, prefsJson))
                {
                    var pushTitle = (dto.Action ?? string.Empty).ToUpperInvariant();
                    var pushBody = TelegramAuditNotifyHelper.StripInternalTags(dto.Description);
                    if (pushBody.Length > 280)
                        pushBody = pushBody[..280] + "…";
                    var tag = "neopos-boss-" + pushKind;
                    await _bossWebPush.NotifyCompanySubscribersAsync(
                        dto.CompanyId,
                        pushTitle,
                        pushBody,
                        "/boss/audit-logs",
                        tag);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WebPush audit xətası");
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

        // Datada vaxtlar local(Baku) kimi saxlanır (Unspecified). Buna görə UTC çevirmirik.
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

        // OrderService: "MƏHSUL SİLİNDİ ❗" — emoji fərqləri üçün Contains
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