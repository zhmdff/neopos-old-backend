using BusinessLayer.Services.Abstractions;
using DAL.Server.Context;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using WebPush;

namespace BusinessLayer.Services.Implementations;

public class BossWebPushService : IBossWebPushService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BossWebPushService> _logger;

    public BossWebPushService(
        AppDbContext db,
        IConfiguration configuration,
        ILogger<BossWebPushService> logger)
    {
        _db = db;
        _configuration = configuration;
        _logger = logger;
    }

    public string? GetVapidPublicKey()
    {
        var k = _configuration["WebPush:PublicKey"]?.Trim();
        return string.IsNullOrEmpty(k) ? null : k;
    }

    public async Task UpsertSubscriptionAsync(
        Guid userId,
        Guid companyId,
        string endpoint,
        string p256dh,
        string auth,
        CancellationToken ct = default)
    {
        endpoint = endpoint.Trim();
        if (string.IsNullOrEmpty(endpoint)) return;

        var existing = await _db.BossWebPushSubscriptions
            .FirstOrDefaultAsync(x => x.Endpoint == endpoint, ct);

        var baku = BakuNow();

        if (existing != null)
        {
            existing.UserId = userId;
            existing.CompanyId = companyId;
            existing.P256dh = p256dh;
            existing.Auth = auth;
            existing.CreatedAt = baku;
        }
        else
        {
            await _db.BossWebPushSubscriptions.AddAsync(new BossWebPushSubscription
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CompanyId = companyId,
                Endpoint = endpoint,
                P256dh = p256dh,
                Auth = auth,
                CreatedAt = baku
            }, ct);
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoveByEndpointAsync(Guid userId, Guid companyId, string endpoint, CancellationToken ct = default)
    {
        endpoint = endpoint.Trim();
        var row = await _db.BossWebPushSubscriptions
            .FirstOrDefaultAsync(x => x.Endpoint == endpoint && x.UserId == userId && x.CompanyId == companyId, ct);
        if (row == null) return;
        _db.BossWebPushSubscriptions.Remove(row);
        await _db.SaveChangesAsync(ct);
    }

    public async Task NotifyCompanySubscribersAsync(
        Guid companyId,
        string title,
        string body,
        string relativeUrl = "/boss/audit-logs",
        string? notificationTag = null,
        CancellationToken ct = default)
    {
        var publicKey = _configuration["WebPush:PublicKey"]?.Trim();
        var privateKey = _configuration["WebPush:PrivateKey"]?.Trim();
        var subject = _configuration["WebPush:Subject"]?.Trim();
        if (string.IsNullOrEmpty(publicKey) || string.IsNullOrEmpty(privateKey) || string.IsNullOrEmpty(subject))
            return;

        var subs = await _db.BossWebPushSubscriptions
            .AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .ToListAsync(ct);

        if (subs.Count == 0) return;

        var url = (relativeUrl ?? "/").Trim();
        if (url.Length == 0) url = "/";
        if (!url.StartsWith('/')) url = "/" + url;

        var tag = string.IsNullOrWhiteSpace(notificationTag) ? "neopos-boss-audit" : notificationTag.Trim();

        var payload = JsonSerializer.Serialize(new { title, body, url, tag });
        var vapid = new VapidDetails(subject, publicKey, privateKey);
        var client = new WebPushClient();

        var sentOk = 0;
        foreach (var s in subs)
        {
            try
            {
                var pushSub = new PushSubscription(s.Endpoint, s.P256dh, s.Auth);
                await client.SendNotificationAsync(pushSub, payload, vapid);
                sentOk++;
            }
            catch (WebPushException ex)
            {
                var code = ex.StatusCode;
                if (code == HttpStatusCode.Gone || code == HttpStatusCode.NotFound)
                {
                    await RemoveSubscriptionByEndpointInternalAsync(s.Endpoint, ct);
                }
                else
                {
                    _logger.LogWarning(ex, "WebPush göndərmə xətası: {Endpoint}", s.Endpoint);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "WebPush göndərmə xətası: {Endpoint}", s.Endpoint);
            }
        }

        if (sentOk > 0)
        {
            _logger.LogInformation(
                "WebPush bildiriş getdi: {SentOk}/{Total} cihaz, CompanyId={CompanyId}, Tag={Tag}, Title={Title}",
                sentOk,
                subs.Count,
                companyId,
                tag,
                title);
        }
    }

    private async Task RemoveSubscriptionByEndpointInternalAsync(string endpoint, CancellationToken ct)
    {
        var row = await _db.BossWebPushSubscriptions.FirstOrDefaultAsync(x => x.Endpoint == endpoint, ct);
        if (row == null) return;
        _db.BossWebPushSubscriptions.Remove(row);
        await _db.SaveChangesAsync(ct);
    }

    private static DateTime BakuNow()
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Azerbaijan Standard Time");
        var t = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        return DateTime.SpecifyKind(t, DateTimeKind.Unspecified);
    }
}
