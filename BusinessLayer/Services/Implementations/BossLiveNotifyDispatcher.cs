using BusinessLayer.DTOs.Audit;
using BusinessLayer.Hubs;
using BusinessLayer.Services.Abstractions;
using BusinessLayer.Utilities;
using DAL.Server.Context;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessLayer.Services.Implementations;

public class BossLiveNotifyDispatcher : IBossLiveNotifyDispatcher
{
    private readonly AppDbContext _context;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly IBossWebPushService _bossWebPush;
    private readonly IBossTelegramNotifyService _bossTelegramNotify;
    private readonly ILogger<BossLiveNotifyDispatcher> _logger;

    private static DateTime AzTime =>
        DateTime.SpecifyKind(DateTime.UtcNow.AddHours(4), DateTimeKind.Unspecified);

    public BossLiveNotifyDispatcher(
        AppDbContext context,
        IHubContext<NotificationHub> hubContext,
        IBossWebPushService bossWebPush,
        IBossTelegramNotifyService bossTelegramNotify,
        ILogger<BossLiveNotifyDispatcher> logger)
    {
        _context = context;
        _hubContext = hubContext;
        _bossWebPush = bossWebPush;
        _bossTelegramNotify = bossTelegramNotify;
        _logger = logger;
    }

    public async Task DispatchAuditAsync(Guid companyId, AuditLogPostDto dto, CancellationToken ct = default)
    {
        var azTime = AzTime;

        try
        {
            var groupKey = companyId.ToString("D").ToLowerInvariant();
            await _hubContext.Clients.Group(groupKey)
                .SendAsync("ReceiveNotification", new
                {
                    title = dto.Action.ToUpper(),
                    body = TelegramAuditNotifyHelper.StripInternalTags(dto.Description),
                    tableName = dto.TableName,
                    hallName = dto.HallName,
                    userName = dto.UserName,
                    time = azTime.ToString("HH:mm")
                }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR bildiriş xətası");
        }

        try
        {
            await _bossTelegramNotify.TryNotifyAuditAsync(
                companyId,
                dto.Action ?? string.Empty,
                dto.Description,
                dto.UserName,
                dto.TableName,
                dto.HallName,
                azTime.ToString("HH:mm"),
                azTime,
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Telegram audit bildiriş xətası");
        }

        try
        {
            var pushKind = TelegramAuditNotifyHelper.ClassifyKind(dto.Action, dto.Description);
            if (string.IsNullOrEmpty(pushKind))
                return;

            var prefsJson = await _context.Companies.AsNoTracking()
                .Where(c => c.Id == companyId)
                .Select(c => c.TelegramNotifyPrefsJson)
                .FirstOrDefaultAsync(ct);
            if (!TelegramAuditNotifyHelper.IsKindEnabled(pushKind, prefsJson))
                return;

            var pushTitle = (dto.Action ?? string.Empty).ToUpperInvariant();
            var pushBody = TelegramAuditNotifyHelper.StripInternalTags(dto.Description);
            if (pushBody.Length > 280)
                pushBody = pushBody[..280] + "…";

            await _bossWebPush.NotifyCompanySubscribersAsync(
                companyId,
                pushTitle,
                pushBody,
                "/boss/audit-logs",
                "neopos-boss-" + pushKind,
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WebPush audit xətası");
        }
    }

    public async Task DispatchPendingDeleteRefreshAsync(Guid companyId, CancellationToken ct = default)
    {
        var groupKey = companyId.ToString("D").ToLowerInvariant();
        try
        {
            await _hubContext.Clients.Group(groupKey).SendAsync("ReceivePendingDeleteRefresh", new { }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR ReceivePendingDeleteRefresh xətası");
        }
    }

    public async Task DispatchPendingDeletePushAsync(
        Guid companyId,
        string pendingId,
        string title,
        string body,
        CancellationToken ct = default)
    {
        await DispatchPendingDeleteRefreshAsync(companyId, ct);

        try
        {
            await _bossWebPush.NotifyCompanySubscribersAsync(
                companyId,
                title,
                body,
                "/boss/dashboard",
                $"neopos-pd-{pendingId}",
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WebPush pending delete xətası");
        }
    }
}
