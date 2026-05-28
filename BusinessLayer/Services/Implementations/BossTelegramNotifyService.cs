using BusinessLayer.Services.Abstractions;
using BusinessLayer.Utilities;
using DAL.Server.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Net.Http;

namespace BusinessLayer.Services.Implementations;

public class BossTelegramNotifyService : IBossTelegramNotifyService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BossTelegramNotifyService> _logger;

    public BossTelegramNotifyService(
        AppDbContext db,
        IConfiguration configuration,
        ILogger<BossTelegramNotifyService> logger)
    {
        _db = db;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task TryNotifyAuditAsync(
        Guid companyId,
        string action,
        string? description,
        string? userName,
        string? tableName,
        string? hallName,
        string timeHHmm,
        DateTime whenLocal,
        CancellationToken ct = default)
    {
        var kind = TelegramAuditNotifyHelper.ClassifyKind(action, description);
        if (string.IsNullOrEmpty(kind)) return;

        var prefsJson = await _db.Companies.AsNoTracking()
            .Where(c => c.Id == companyId)
            .Select(c => c.TelegramNotifyPrefsJson)
            .FirstOrDefaultAsync(ct);
        if (!TelegramAuditNotifyHelper.IsKindEnabled(kind, prefsJson)) return;

        var token = await ResolveBossTelegramBotTokenAsync(companyId, ct);
        if (string.IsNullOrEmpty(token)) return;

        var chatIds = await GetMergedTelegramRecipientChatIdsAsync(companyId, ct);
        if (chatIds.Count == 0) return;

        var text = TelegramAuditNotifyHelper.BuildMessage(
            kind, action, description, userName, tableName, hallName, timeHHmm, whenLocal);
        if (text.Length > 4000) text = text[..4000];

        var url = $"https://api.telegram.org/bot{token}/sendMessage";
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        foreach (var chatId in chatIds)
        {
            try
            {
                var post = new Dictionary<string, string>
                {
                    ["chat_id"] = chatId.ToString(CultureInfo.InvariantCulture),
                    ["text"] = text,
                };
                using var content = new FormUrlEncodedContent(post);
                using var resp = await http.PostAsync(url, content, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    var err = await resp.Content.ReadAsStringAsync(ct);
                    _logger.LogWarning("Telegram audit sendMessage uğursuz: {Status} {Body}", resp.StatusCode, err);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Telegram audit bildirişi ChatId={ChatId}", chatId);
            }
        }
    }

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
}
