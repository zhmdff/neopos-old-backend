using System.Net.Http.Json;
using BusinessLayer.DTOs.Audit;
using BusinessLayer.DTOs.System;
using BusinessLayer.Services.Abstractions;
using BusinessLayer.Utilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BusinessLayer.Services.Implementations;

public class BossMasterNotifyRelayService : IBossMasterNotifyRelayService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BossMasterNotifyRelayService> _logger;

    public BossMasterNotifyRelayService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<BossMasterNotifyRelayService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public Task TryRelayAuditAsync(AuditLogPostDto dto, CancellationToken ct = default)
    {
        if (!ShouldRelayAudit(dto))
            return Task.CompletedTask;

        return PostRelayAsync(new BossNotifyRelayRequestDto
        {
            Kind = "audit",
            Audit = dto,
        }, ct);
    }

    public Task TryRelayPendingDeleteAsync(
        Guid companyId,
        string pendingId,
        string title,
        string body,
        CancellationToken ct = default)
    {
        _ = companyId;
        return PostRelayAsync(new BossNotifyRelayRequestDto
        {
            Kind = "pendingDeletePush",
            PendingDeletePush = new BossPendingDeletePushRelayDto
            {
                PendingId = pendingId,
                Title = title,
                Body = body,
                RelativeUrl = "/boss/dashboard",
            },
        }, ct);
    }

    private static bool ShouldRelayAudit(AuditLogPostDto dto) =>
        !string.IsNullOrEmpty(TelegramAuditNotifyHelper.ClassifyKind(dto.Action, dto.Description));

    private async Task PostRelayAsync(BossNotifyRelayRequestDto request, CancellationToken ct)
    {
        if (!IsTenantMode())
            return;

        var tenantKey = _configuration["NeoPos:TenantKey"]?.Trim();
        if (string.IsNullOrEmpty(tenantKey) || tenantKey == "YOUR_TENANT_KEY_HERE")
            return;

        var baseUrl = ResolveMasterWebBaseUrl();
        var secret = ResolveSyncSecret();
        if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(secret))
            return;

        request.TenantKey = tenantKey;

        try
        {
            var client = _httpClientFactory.CreateClient("BossMasterNotifyRelay");
            using var msg = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/System/relay-boss-notify");
            msg.Headers.TryAddWithoutValidation("X-NeoPos-Sync-Secret", secret);
            msg.Content = JsonContent.Create(request);

            var response = await client.SendAsync(msg, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Boss notify relay to master failed: {Status} ({Kind})",
                    (int)response.StatusCode,
                    request.Kind);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Boss notify relay to master error ({Kind})", request.Kind);
        }
    }

    private bool IsTenantMode()
    {
        var mode = Environment.GetEnvironmentVariable("NEOPOS_MODE")
                   ?? _configuration["NeoPos:Mode"]
                   ?? "tenant";
        return !mode.Equals("master", StringComparison.OrdinalIgnoreCase);
    }

    private string? ResolveMasterWebBaseUrl()
    {
        var configured = _configuration["Sync:MasterWebBaseUrl"]?.Trim();
        if (!string.IsNullOrEmpty(configured))
            return configured.TrimEnd('/');

        var env = Environment.GetEnvironmentVariable("NEOPOS_MASTER_WEB_URL")?.Trim();
        return string.IsNullOrEmpty(env) ? null : env.TrimEnd('/');
    }

    private string? ResolveSyncSecret()
    {
        var secret = _configuration["Sync:MediaUploadSecret"]?.Trim();
        if (!string.IsNullOrEmpty(secret))
            return secret;

        return _configuration["NeoPos:TenantBootstrapSecret"]?.Trim();
    }
}
