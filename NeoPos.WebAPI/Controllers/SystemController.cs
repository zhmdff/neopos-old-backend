using Microsoft.AspNetCore.Mvc;
using NeoPos.WebAPI.Services;
using BusinessLayer.DTOs.System;
using BusinessLayer.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace NeoPos.WebAPI.Controllers;

/// <summary>Ingest upload files from tenant terminals during sync (master / cloud).</summary>

/// <summary>Ümumi sistem məlumatı (terminal üçün server vaxtı və s.).</summary>
[ApiController]
[Route("api/[controller]")]
public class SystemController : ControllerBase
{
    private readonly DAL.Server.Context.AppDbContext _localDb;
    private readonly DAL.Server.Context.RemoteDbContext? _remoteDb;
    private readonly IBossLiveNotifyDispatcher _bossLiveNotify;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SystemController> _logger;

    public SystemController(
        DAL.Server.Context.AppDbContext localDb,
        IServiceProvider serviceProvider,
        IBossLiveNotifyDispatcher bossLiveNotify,
        IWebHostEnvironment env,
        IConfiguration configuration,
        ILogger<SystemController> logger)
    {
        _localDb = localDb;
        _remoteDb = serviceProvider.GetService<DAL.Server.Context.RemoteDbContext>();
        _bossLiveNotify = bossLiveNotify;
        _env = env;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Azərbaycan (Bakı) saat zonası ilə cari vaxt — kompüter saatından asılı olmayaraq server UTC əsasındandır.
    /// </summary>
    [HttpGet("baku-now")]
    public IActionResult BakuNow()
    {
        TimeZoneInfo tz;
        try
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Baku");
        }
        catch
        {
            try
            {
                tz = TimeZoneInfo.FindSystemTimeZoneById("Azerbaijan Standard Time");
            }
            catch
            {
                return StatusCode(500, new { message = "Saat zonası tapılmadı (Asia/Baku)." });
            }
        }

        var utc = DateTime.UtcNow;
        var now = TimeZoneInfo.ConvertTimeFromUtc(utc, tz);
        return Ok(new
        {
            timeZoneId = tz.Id,
            iso = now.ToString("yyyy-MM-ddTHH:mm:ss"),
            date = now.ToString("yyyy-MM-dd"),
            hour = now.Hour,
            minute = now.Minute,
            totalMinutes = now.Hour * 60 + now.Minute,
            utcIso = utc.ToString("o"),
        });
    }

    [HttpGet("sync-status")]
    public async Task<IActionResult> GetSyncStatus()
    {
        var metadata = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(_localDb.LocalSyncMetadata);
        
        // Count unsynced items across all tables
        var pendingCount = 0;
        var dbSetProperties = typeof(DAL.Server.Context.AppDbContext)
            .GetProperties()
            .Where(p => p.PropertyType.IsGenericType && 
                        p.PropertyType.GetGenericTypeDefinition() == typeof(Microsoft.EntityFrameworkCore.DbSet<>));

        foreach (var prop in dbSetProperties)
        {
            var entityType = prop.PropertyType.GetGenericArguments()[0];
            if (typeof(Domain.Common.BaseEntity).IsAssignableFrom(entityType))
            {
                var method = typeof(SystemController)
                    .GetMethod(nameof(GetPendingCountForTable), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.MakeGenericMethod(entityType);
                
                if (method != null)
                {
                    pendingCount += await (Task<int>)method.Invoke(this, new object[] { _localDb })!;
                }
            }
        }

        bool isRemoteReachable = false;
        try
        {
            if (_remoteDb != null)
                isRemoteReachable = await _remoteDb.Database.CanConnectAsync();
        }
        catch { }

        return Ok(new
        {
            lastSuccessfulSyncAt = metadata?.LastSuccessfulSyncAt,
            lastSyncStatus = metadata?.LastSyncStatus,
            pendingCount = pendingCount,
            isRemoteReachable = isRemoteReachable,
            isOutageSimulated = DatabaseSyncService.IsOutageSimulated
        });
    }

    private async Task<int> GetPendingCountForTable<T>(DAL.Server.Context.AppDbContext localDb) where T : Domain.Common.BaseEntity
    {
        return await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(
            Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AsNoTracking(localDb.Set<T>()), 
            x => !x.IsSynced);
    }

    [HttpPost("toggle-outage")]
    public IActionResult ToggleOutage([FromQuery] bool enabled)
    {
        DatabaseSyncService.IsOutageSimulated = enabled;
        return Ok(new { isOutageSimulated = enabled });
    }

    /// <summary>
    /// Tenant sync pushes binary files here (PUT). Requires <c>X-NeoPos-Sync-Secret</c> matching
    /// <c>Sync:MediaUploadSecret</c> or <c>NeoPos:TenantBootstrapSecret</c> on the master server.
    /// </summary>
    [HttpPut("sync-media")]
    [RequestSizeLimit(52_428_800)]
    public async Task<IActionResult> PutSyncMedia(
        [FromHeader(Name = "X-NeoPos-Sync-Secret")] string? secret,
        [FromQuery] string path,
        CancellationToken cancellationToken)
    {
        if (!ValidateMediaSyncSecret(secret))
            return Unauthorized(new { message = "Invalid sync secret." });

        var normalized = MediaPathCollector.NormalizeOne(path);
        if (normalized == null)
            return BadRequest(new { message = "Path must be under /uploads/ and must not contain '..'." });

        var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var localPath = MediaPathCollector.ToLocalFilePath(webRoot, normalized);

        try
        {
            var dir = Path.GetDirectoryName(localPath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            await using (var fs = System.IO.File.Create(localPath))
            {
                await Request.Body.CopyToAsync(fs, cancellationToken);
            }

            _logger.LogInformation("Sync media received: {Path}", normalized);
            return Ok(new { path = normalized });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save sync media: {Path}", normalized);
            return StatusCode(500, new { message = "Could not save file.", error = ex.Message });
        }
    }

    private bool ValidateMediaSyncSecret(string? secret)
    {
        var expected = _configuration["Sync:MediaUploadSecret"]?.Trim();
        if (string.IsNullOrEmpty(expected))
            expected = _configuration["NeoPos:TenantBootstrapSecret"]?.Trim();
        if (string.IsNullOrEmpty(expected))
            return false;
        return string.Equals(expected, secret?.Trim(), StringComparison.Ordinal);
    }

    [HttpPost("relay-boss-notify")]
    public async Task<IActionResult> RelayBossNotify(
        [FromHeader(Name = "X-NeoPos-Sync-Secret")] string? secret,
        [FromBody] BossNotifyRelayRequestDto dto,
        CancellationToken cancellationToken)
    {
        if (!ValidateMediaSyncSecret(secret))
            return Unauthorized(new { message = "Invalid sync secret." });

        var tenantKey = (dto.TenantKey ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(tenantKey))
            return BadRequest(new { message = "TenantKey is required." });

        var company = await _localDb.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantKey == tenantKey && !c.IsDeleted, cancellationToken);
        if (company == null)
            return NotFound(new { message = "Company not found for TenantKey." });

        var kind = (dto.Kind ?? "audit").Trim().ToLowerInvariant();
        try
        {
            switch (kind)
            {
                case "audit":
                    if (dto.Audit == null)
                        return BadRequest(new { message = "Audit payload is required." });
                    dto.Audit.CompanyId = company.Id;
                    await _bossLiveNotify.DispatchAuditAsync(company.Id, dto.Audit, cancellationToken);
                    break;

                case "pendingdeletepush":
                    var push = dto.PendingDeletePush;
                    if (push == null || string.IsNullOrWhiteSpace(push.PendingId))
                        return BadRequest(new { message = "PendingDeletePush payload is required." });
                    await _bossLiveNotify.DispatchPendingDeletePushAsync(
                        company.Id,
                        push.PendingId.Trim(),
                        push.Title ?? "Silinmə təsdiqi",
                        push.Body ?? string.Empty,
                        cancellationToken);
                    break;

                case "pendingdeleterefresh":
                    await _bossLiveNotify.DispatchPendingDeleteRefreshAsync(company.Id, cancellationToken);
                    break;

                default:
                    return BadRequest(new { message = $"Unknown kind: {dto.Kind}" });
            }

            return Ok(new { message = "Relayed." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "relay-boss-notify failed for TenantKey {TenantKey}", tenantKey);
            return StatusCode(500, new { message = "Relay failed.", error = ex.Message });
        }
    }

    [HttpPost("trigger-sync")]
    public async Task<IActionResult> TriggerSync([FromServices] IServiceProvider serviceProvider)
    {
        var syncService = serviceProvider.GetService<DatabaseSyncService>();
        if (syncService == null)
        {
            return BadRequest(new
            {
                message = "Sinxronizasiya yalnız tenant rejimində (lokal SQLite + Neon) işləyir. " +
                          "POS terminalında NeoPos:Mode=tenant və ya NEOPOS_MODE=tenant təyin edin; " +
                          "master rejimi yalnız bulud server üçündür.",
            });
        }

        try
        {
            await syncService.TriggerSyncAsync(HttpContext.RequestAborted);
            return Ok(new { message = "Sinxronizasiya tamamlandı." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Sinxronizasiya zamanı xəta baş verdi.", error = ex.Message });
        }
    }
}
