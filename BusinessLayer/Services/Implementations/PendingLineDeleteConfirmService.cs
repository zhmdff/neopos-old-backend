using BusinessLayer.DTOs.PendingLineDelete;
using BusinessLayer.Hubs;
using BusinessLayer.Services.Abstractions;
using DAL.Server.Context;
using Domain.Entities;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace BusinessLayer.Services.Implementations;

public class PendingLineDeleteConfirmService : IPendingLineDeleteConfirmService
{
    private readonly AppDbContext _db;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly IBossWebPushService _bossWebPush;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PendingLineDeleteConfirmService> _logger;

    public PendingLineDeleteConfirmService(
        AppDbContext db,
        IHubContext<NotificationHub> hubContext,
        IBossWebPushService bossWebPush,
        IConfiguration configuration,
        ILogger<PendingLineDeleteConfirmService> logger)
    {
        _db = db;
        _hubContext = hubContext;
        _bossWebPush = bossWebPush;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task RegisterAsync(Guid companyId, PendingLineDeleteRegisterDto dto, CancellationToken ct = default)
    {
        var pendingId = (dto.PendingId ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(pendingId))
            throw new ArgumentException("PendingId tələb olunur.");

        var existing = await _db.PendingOrderLineDeleteConfirms
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.PendingId == pendingId, ct);
        if (existing != null)
            return;

        var detail = await _db.OrderDetails
            .AsNoTracking()
            .Include(d => d.OrderHeader)
            .FirstOrDefaultAsync(d => d.Id == dto.OrderLineId && d.OrderHeaderId == dto.OrderId, ct);

        if (detail?.OrderHeader == null || detail.OrderHeader.CompanyId != companyId)
            throw new InvalidOperationException("Sifariş sətri tapılmadı və ya şirkət uyğun deyil.");

        var exp = dto.ExpiresAtUtc ?? DateTime.UtcNow.AddMinutes(10);
        if (exp <= DateTime.UtcNow)
            exp = DateTime.UtcNow.AddMinutes(10);

        static string? TruncOpt(string? s, int max)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var t = s.Trim();
            return t.Length <= max ? t : t.Substring(0, max);
        }

        static string TruncReq(string? s, int max, string fallback)
        {
            if (string.IsNullOrWhiteSpace(s)) return fallback;
            var t = s.Trim();
            return t.Length <= max ? t : t.Substring(0, max);
        }

        var row = new PendingOrderLineDeleteConfirm
        {
            Id = Guid.NewGuid(),
            IsDeleted = false,
            CompanyId = companyId,
            PendingId = pendingId,
            OrderHeaderId = dto.OrderId,
            OrderDetailId = dto.OrderLineId,
            TableName = TruncOpt(dto.TableName, 200),
            ProductName = TruncReq(dto.ProductName, 500, "Məhsul"),
            Quantity = dto.Quantity,
            ReasonSnapshot = TruncOpt(dto.ReasonSnapshot, 2000),
            RequestedByDisplayName = TruncOpt(dto.RequestedByDisplayName, 200),
            ExpiresAtUtc = exp,
            Status = 0,
            CreatedAtUtc = DateTime.UtcNow,
        };

        await _db.PendingOrderLineDeleteConfirms.AddAsync(row, ct);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Pending delete duplicate insert: {PendingId}", pendingId);
            return;
        }

        var groupKey = companyId.ToString("D").ToLowerInvariant();
        try
        {
            await _hubContext.Clients.Group(groupKey).SendAsync("ReceivePendingDeleteRefresh", new { }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR ReceivePendingDeleteRefresh xətası");
        }

        try
        {
            var table = row.TableName ?? "Masa";
            var body = $"{table}: {row.ProductName} × {row.Quantity} — ofisiant proqramında bu çek açıq olanda «Bəli» / «Xeyr» düymələri və ya Telegram təsdiqi.";
            await _bossWebPush.NotifyCompanySubscribersAsync(
                companyId,
                "Silinmə təsdiqi",
                body,
                "/boss/dashboard",
                $"neopos-pd-{pendingId}",
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WebPush pending delete xətası");
        }

        await NotifyBossTelegramChatsAsync(companyId, row, ct);
    }

    public async Task<List<PendingLineDeleteActiveItemDto>> GetActiveAsync(Guid companyId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return await _db.PendingOrderLineDeleteConfirms.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.Status == 0 && x.ExpiresAtUtc > now)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new PendingLineDeleteActiveItemDto
            {
                PendingId = x.PendingId,
                OrderId = x.OrderHeaderId,
                OrderLineId = x.OrderDetailId,
                TableName = x.TableName,
                ProductName = x.ProductName,
                Quantity = x.Quantity,
                ReasonSnapshot = x.ReasonSnapshot,
                ExpiresAtUtc = x.ExpiresAtUtc,
                CreatedAtUtc = x.CreatedAtUtc,
            })
            .ToListAsync(ct);
    }

    public async Task<(string status, bool? accepted)> GetStatusAsync(Guid companyId, string pendingId, CancellationToken ct = default)
    {
        pendingId = (pendingId ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(pendingId))
            return ("not_found", null);

        var row = await _db.PendingOrderLineDeleteConfirms.AsNoTracking()
            .FirstOrDefaultAsync(x => x.PendingId == pendingId && x.CompanyId == companyId, ct);
        if (row == null)
            return ("not_found", null);

        if (row.Status == 1)
            return ("accepted", true);
        if (row.Status == 2)
            return ("rejected", false);

        if (row.ExpiresAtUtc <= DateTime.UtcNow)
            return ("expired", false);

        return ("pending", null);
    }

    public async Task<(string status, bool? accepted)> TryResolveAsync(Guid companyId, string pendingId, bool accepted, CancellationToken ct = default)
    {
        pendingId = (pendingId ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(pendingId))
            return ("not_found", null);

        var row = await _db.PendingOrderLineDeleteConfirms
            .FirstOrDefaultAsync(x => x.PendingId == pendingId && x.CompanyId == companyId, ct);
        if (row == null)
            return ("not_found", null);

        if (row.Status == 1)
            return ("accepted", true);
        if (row.Status == 2)
            return ("rejected", false);

        if (row.ExpiresAtUtc <= DateTime.UtcNow)
        {
            row.Status = 2;
            await _db.SaveChangesAsync(ct);
            return ("expired", false);
        }

        var newStatus = (byte)(accepted ? 1 : 2);
        var now = DateTime.UtcNow;
        var affected = await _db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE "PendingOrderLineDeleteConfirms"
            SET "Status" = {newStatus}
            WHERE "Id" = {row.Id} AND "Status" = 0 AND "ExpiresAtUtc" > {now}
            """,
            ct);

        if (affected == 0)
        {
            await _db.Entry(row).ReloadAsync(ct);
            if (row.Status == 1)
                return ("accepted", true);
            if (row.Status == 2)
                return ("rejected", false);
            return ("pending", null);
        }

        await ReplaceTelegramConfirmOutcomeAsync(companyId, row.Id, accepted, ct);

        var groupKey = companyId.ToString("D").ToLowerInvariant();
        try
        {
            await _hubContext.Clients.Group(groupKey).SendAsync("ReceivePendingDeleteRefresh", new { }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR after resolve");
        }

        return accepted ? ("accepted", true) : ("rejected", false);
    }

    public async Task<(string status, bool? accepted)> TryResolveByPendingIdAsync(
        string pendingId,
        bool accepted,
        CancellationToken ct = default)
    {
        pendingId = (pendingId ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(pendingId))
            return ("not_found", null);

        var row = await _db.PendingOrderLineDeleteConfirms.AsNoTracking()
            .FirstOrDefaultAsync(x => x.PendingId == pendingId, ct);
        if (row == null)
            return ("not_found", null);

        return await TryResolveAsync(row.CompanyId, pendingId, accepted, ct);
    }

    /// <summary>
    /// Şirkətə bağlı BossTelegramChats ünvanlarına mətn (parse_mode HTML).
    /// Token: appsettings «BossTelegram:BotToken»; boşdursa atlanır.
    /// </summary>
    private static IEnumerable<long> ReadExtraTelegramChatIdsFromConfig(IConfiguration configuration, Guid companyId)
    {
        var key = companyId.ToString("D", CultureInfo.InvariantCulture);
        var raw = configuration[$"BossTelegram:ExtraChats:{key}"]?.Trim();
        if (string.IsNullOrEmpty(raw)) yield break;
        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (long.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) && id != 0)
                yield return id;
        }
    }

    private async Task<List<long>> GetMergedTelegramRecipientChatIdsAsync(Guid companyId, CancellationToken ct)
    {
        var fromDb = await _db.BossTelegramChats.AsNoTracking()
            .Where(x => x.CompanyId == companyId)
            .Select(x => x.ChatId)
            .Distinct()
            .ToListAsync(ct);
        var set = new HashSet<long>(fromDb);
        foreach (var id in ReadExtraTelegramChatIdsFromConfig(_configuration, companyId))
            set.Add(id);
        return set.ToList();
    }

    private async Task<string?> ResolveBossTelegramBotTokenAsync(Guid companyId, CancellationToken ct)
    {
        var cfg = _configuration["BossTelegram:BotToken"]?.Trim();
        if (!string.IsNullOrEmpty(cfg))
            return cfg;

        var fromCompany = await _db.Companies.AsNoTracking()
            .Where(c => c.Id == companyId)
            .Select(c => c.TelegramBotToken)
            .FirstOrDefaultAsync(ct);
        var t = fromCompany?.Trim();
        return string.IsNullOrEmpty(t) ? null : t;
    }

    private async Task NotifyBossTelegramChatsAsync(Guid companyId, PendingOrderLineDeleteConfirm row, CancellationToken ct)
    {
        var token = await ResolveBossTelegramBotTokenAsync(companyId, ct);
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogInformation(
                "Boss Telegram: silinmə təsdiqi mesajı göndərilmədi (BossTelegram:BotToken və şirkət TelegramBotToken boş). CompanyId={CompanyId}, PendingId={PendingId}",
                companyId,
                row.PendingId);
            return;
        }

        var chatIds = await GetMergedTelegramRecipientChatIdsAsync(companyId, ct);
        if (chatIds.Count == 0)
        {
            _logger.LogWarning(
                "Boss Telegram: BotToken var, alıcı chat yoxdur — mesaj göndərilmədi. Boss paneldə «Şirkət ayarları» → Telegram bölməsində chat əlavə edin və ya appsettings-də BossTelegram:ExtraChats:<şirkət_guid> (vergüllə bir neçə id). CompanyId={CompanyId}, PendingId={PendingId}",
                companyId,
                row.PendingId);
            return;
        }

        var table = row.TableName ?? "Masa";
        var reason = string.IsNullOrWhiteSpace(row.ReasonSnapshot) ? "" : $"\n{System.Net.WebUtility.HtmlEncode(row.ReasonSnapshot)}";
        var who = string.IsNullOrWhiteSpace(row.RequestedByDisplayName)
            ? ""
            : $"\n<b>Sorğunu göndərdən:</b> {System.Net.WebUtility.HtmlEncode(row.RequestedByDisplayName.Trim())}";
        var text =
            $"🔔 <b>Silinmə təsdiqi</b>\n{System.Net.WebUtility.HtmlEncode(table)}: " +
            $"{System.Net.WebUtility.HtmlEncode(row.ProductName)} × {row.Quantity.ToString(CultureInfo.InvariantCulture)}" +
            $"{reason}{who}\n\n" +
            "<b>Bəli</b> / <b>Xeyr</b> — bu mesajın altındakı düymələrdən və ya Boss paneldə «Gözləyən silinmələr».";
        if (text.Length > 4000)
            text = text[..4000];

        var replyMarkupJson = JsonSerializer.Serialize(new
        {
            inline_keyboard = new[]
            {
                new[]
                {
                    new { text = "Bəli", callback_data = $"neo_sd1|{row.PendingId}" },
                    new { text = "Xeyr", callback_data = $"neo_sd0|{row.PendingId}" },
                },
            },
        });

        var url = $"https://api.telegram.org/bot{token}/sendMessage";
        var refs = new Dictionary<string, int>();

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        foreach (var chatId in chatIds)
        {
            try
            {
                var post = new Dictionary<string, string>
                {
                    ["chat_id"] = chatId.ToString(CultureInfo.InvariantCulture),
                    ["text"] = text,
                    ["parse_mode"] = "HTML",
                    ["reply_markup"] = replyMarkupJson,
                };
                using var content = new FormUrlEncodedContent(post);
                using var resp = await http.PostAsync(url, content, ct);
                var respBody = await resp.Content.ReadAsStringAsync(ct);
                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Telegram sendMessage uğursuz: {Status} {Body}", resp.StatusCode, respBody);
                    continue;
                }

                try
                {
                    using var doc = JsonDocument.Parse(respBody);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("ok", out var okEl) && okEl.ValueKind == JsonValueKind.True &&
                        root.TryGetProperty("result", out var result) &&
                        result.TryGetProperty("message_id", out var midEl) &&
                        midEl.TryGetInt32(out var mid) &&
                        mid > 0)
                    {
                        refs[chatId.ToString(CultureInfo.InvariantCulture)] = mid;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Telegram sendMessage cavabı parse olunmadı ChatId={ChatId}", chatId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Boss Telegram bildirişi xətası ChatId={ChatId}", chatId);
            }
        }

        if (refs.Count > 0)
        {
            var refsJson = JsonSerializer.Serialize(refs);
            await _db.PendingOrderLineDeleteConfirms
                .Where(x => x.Id == row.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.TelegramConfirmMessageRefsJson, refsJson), ct);
        }
    }

    private async Task ReplaceTelegramConfirmOutcomeAsync(Guid companyId, Guid rowId, bool accepted, CancellationToken ct)
    {
        var json = await _db.PendingOrderLineDeleteConfirms.AsNoTracking()
            .Where(x => x.Id == rowId)
            .Select(x => x.TelegramConfirmMessageRefsJson)
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(json))
            return;

        Dictionary<string, int>? refs;
        try
        {
            refs = JsonSerializer.Deserialize<Dictionary<string, int>>(json);
        }
        catch
        {
            return;
        }

        if (refs == null || refs.Count == 0)
            return;

        var token = await ResolveBossTelegramBotTokenAsync(companyId, ct);
        if (string.IsNullOrEmpty(token))
            return;

        var text = accepted ? "Təsdiq qəbul edildi." : "Təsdiq qəbul edilmədi.";
        var url = $"https://api.telegram.org/bot{token}/editMessageText";
        var payloadTemplate = new Dictionary<string, object?>
        {
            ["text"] = text,
            ["reply_markup"] = new { inline_keyboard = Array.Empty<object[]>() },
        };

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        foreach (var kv in refs)
        {
            if (!long.TryParse(kv.Key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var chatId))
                continue;
            var mid = kv.Value;
            if (mid <= 0)
                continue;

            var payload = new Dictionary<string, object?>(payloadTemplate)
            {
                ["chat_id"] = chatId,
                ["message_id"] = mid,
            };
            try
            {
                using var content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    System.Text.Encoding.UTF8,
                    "application/json");
                using var resp = await http.PostAsync(url, content, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    var err = await resp.Content.ReadAsStringAsync(ct);
                    _logger.LogWarning("Telegram editMessageText uğursuz: {Status} {Body}", resp.StatusCode, err);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Telegram editMessageText ChatId={ChatId}", chatId);
            }
        }
    }
}
