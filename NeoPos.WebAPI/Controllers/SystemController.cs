using Microsoft.AspNetCore.Mvc;
using NeoPos.WebAPI.Services;

namespace NeoPos.WebAPI.Controllers;

/// <summary>Ümumi sistem məlumatı (terminal üçün server vaxtı və s.).</summary>
[ApiController]
[Route("api/[controller]")]
public class SystemController : ControllerBase
{
    private readonly DAL.Server.Context.AppDbContext _localDb;
    private readonly DAL.Server.Context.RemoteDbContext _remoteDb;

    public SystemController(DAL.Server.Context.AppDbContext localDb, DAL.Server.Context.RemoteDbContext remoteDb)
    {
        _localDb = localDb;
        _remoteDb = remoteDb;
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

    [HttpPost("trigger-sync")]
    public async Task<IActionResult> TriggerSync([FromServices] DatabaseSyncService syncService)
    {
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
