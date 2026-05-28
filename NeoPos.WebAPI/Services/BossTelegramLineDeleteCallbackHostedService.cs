using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using BusinessLayer.Services.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NeoPos.WebAPI.Services;

/// <summary>
/// Eyni bot token ilə Telegram <c>callback_query</c> (neo_sd0| / neo_sd1|) emalı.
/// <b>Diqqət:</b> Windows terminal (Electron) da eyni tokenlə <c>getUpdates</c> işlədirsə, yeniləmələr bölünə bilər —
/// bu halda appsettings-də <c>BossTelegram:PollLineDeleteCallbacks</c> söndürün və ya yalnız serverdə aktiv edin.
/// </summary>
public sealed class BossTelegramLineDeleteCallbackHostedService : BackgroundService
{
    private static readonly Regex NeoSd = new(@"^neo_sd([01])\|(\S+)$", RegexOptions.Compiled);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BossTelegramLineDeleteCallbackHostedService> _logger;

    public BossTelegramLineDeleteCallbackHostedService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<BossTelegramLineDeleteCallbackHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue("BossTelegram:PollLineDeleteCallbacks", false))
        {
            _logger.LogInformation(
                "BossTelegram:PollLineDeleteCallbacks=false — Telegram silinmə düymələri server tərəfindən dinlənilmir (ofisiant/brauzer UI və ya Electron).");
            return;
        }

        var offset = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            var token = _configuration["BossTelegram:BotToken"]?.Trim();
            if (string.IsNullOrEmpty(token))
            {
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                continue;
            }

            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(35) };
                var url =
                    $"https://api.telegram.org/bot{token}/getUpdates?timeout=25&offset={offset.ToString(CultureInfo.InvariantCulture)}";
                using var resp = await http.GetAsync(url, stoppingToken);
                var json = await resp.Content.ReadAsStringAsync(stoppingToken);
                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Telegram getUpdates uğursuz: {Status} {Body}", resp.StatusCode, json);
                    await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
                    continue;
                }

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (!root.TryGetProperty("ok", out var okEl) || !okEl.GetBoolean())
                    continue;

                if (!root.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var u in result.EnumerateArray())
                {
                    if (u.TryGetProperty("update_id", out var uid))
                    {
                        var id = uid.GetInt32();
                        if (id >= offset)
                            offset = id + 1;
                    }

                    if (!u.TryGetProperty("callback_query", out var cq))
                        continue;

                    var cqId = cq.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                    var data = cq.TryGetProperty("data", out var dEl) ? dEl.GetString()?.Trim() : null;
                    if (string.IsNullOrEmpty(cqId) || string.IsNullOrEmpty(data))
                        continue;

                    var m = NeoSd.Match(data);
                    if (!m.Success)
                    {
                        await AnswerCallbackQueryAsync(http, token, cqId, null, stoppingToken);
                        continue;
                    }

                    var yes = m.Groups[1].Value == "1";
                    var pendingId = m.Groups[2].Value.Trim();
                    if (string.IsNullOrEmpty(pendingId))
                    {
                        await AnswerCallbackQueryAsync(http, token, cqId, "PendingId boşdur.", stoppingToken);
                        continue;
                    }

                    string resolveStatus;
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var svc = scope.ServiceProvider.GetRequiredService<IPendingLineDeleteConfirmService>();
                        var (status, _) = await svc.TryResolveByPendingIdAsync(pendingId, yes, stoppingToken);
                        resolveStatus = status;
                    }

                    var hint = resolveStatus switch
                    {
                        "accepted" => "Təsdiqləndi.",
                        "rejected" => "Rədd edildi.",
                        "expired" => "Vaxt bitib.",
                        "not_found" => "Sorğu tapılmadı və ya artıq bağlanıb.",
                        _ => "Gözləyir…",
                    };
                    await AnswerCallbackQueryAsync(http, token, cqId, hint, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "BossTelegram getUpdates döngüsü");
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }
    }

    private static async Task AnswerCallbackQueryAsync(
        HttpClient http,
        string botToken,
        string callbackQueryId,
        string? text,
        CancellationToken ct)
    {
        try
        {
            var url = $"https://api.telegram.org/bot{botToken}/answerCallbackQuery";
            var bodyDict = new Dictionary<string, object?> { ["callback_query_id"] = callbackQueryId };
            if (!string.IsNullOrEmpty(text))
            {
                bodyDict["text"] = text;
                bodyDict["show_alert"] = text.Length > 80;
            }

            var body = JsonSerializer.Serialize(bodyDict);
            using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
            using var r = await http.PostAsync(url, content, ct);
            if (!r.IsSuccessStatusCode)
            {
                _ = await r.Content.ReadAsStringAsync(ct);
            }
        }
        catch
        {
            /* */
        }
    }
}
