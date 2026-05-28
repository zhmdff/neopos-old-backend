using BusinessLayer.DTOs.CashShift;
using BusinessLayer.Services.Abstractions;
using DAL.Server.Context;
using Domain.Common.Entities;
using Microsoft.EntityFrameworkCore;

namespace NeoPos.WebAPI.Services;

/// <summary>
/// Server-side avtomatik kassa növbəsi scheduler-i.
/// Terminal açıq olmasa da işləsin deyə WebAPI prosesində periodik yoxlayır.
/// </summary>
public sealed class AutoCashShiftHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AutoCashShiftHostedService> _logger;

    // Tick aralığı: dəqiqəlik
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(1);

    // Təhlükəsiz pəncərə (dəqiqə): open/close vaxtından sonra neçə dəqiqə ərzində icra et
    private const int WindowMinutes = 5;

    public AutoCashShiftHostedService(IServiceScopeFactory scopeFactory, ILogger<AutoCashShiftHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Startup-da azacıq gecikdirək ki, host tam qalxsın
        try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); } catch { /* ignore */ }

        using var timer = new PeriodicTimer(TickInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickOnce(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[auto-shift] tick failed");
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private static int? ParseHHmmToMinutes(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var parts = s.Trim().Split(':');
        if (parts.Length != 2) return null;
        if (!int.TryParse(parts[0], out var hh)) return null;
        if (!int.TryParse(parts[1], out var mm)) return null;
        if (hh < 0 || hh > 23 || mm < 0 || mm > 59) return null;
        return hh * 60 + mm;
    }

    private static DateTime BakuNowUtcPlus4() => DateTime.UtcNow.AddHours(4);

    private static bool InWindow(int nowMinutes, int targetMinutes)
        => nowMinutes >= targetMinutes && nowMinutes < targetMinutes + WindowMinutes;

    private static Guid? PickActorUserId(List<(Guid UserId, bool IsAdmin, List<int> Permissions)> candidates)
    {
        var admin = candidates.FirstOrDefault(x => x.IsAdmin).UserId;
        if (admin != Guid.Empty) return admin;

        // permission 20: CashShift idarə
        var p20 = candidates.FirstOrDefault(x => x.Permissions.Contains(20)).UserId;
        if (p20 != Guid.Empty) return p20;

        return null;
    }

    private async Task TickOnce(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cashShiftService = scope.ServiceProvider.GetRequiredService<ICashShiftService>();

        // bütün aktiv şirkətlər (AutoCashShiftEnabled=true)
        var companies = await db.Companies
            .AsNoTracking()
            .Where(c => c.IsActive && c.AutoCashShiftEnabled)
            .Select(c => new
            {
                c.Id,
                c.NameAz,
                c.AutoCashShiftOpenTime,
                c.AutoCashShiftCloseTime,
                c.AutoCashShiftForceClose
            })
            .ToListAsync(ct);

        if (companies.Count == 0) return;

        var bakuNow = BakuNowUtcPlus4();
        var nowMinutes = bakuNow.Hour * 60 + bakuNow.Minute;

        foreach (var c in companies)
        {
            ct.ThrowIfCancellationRequested();

            var openM = ParseHHmmToMinutes(c.AutoCashShiftOpenTime);
            var closeM = ParseHHmmToMinutes(c.AutoCashShiftCloseTime);
            if (openM == null || closeM == null || openM.Value == closeM.Value) continue;

            // gecə növbəsi dəstəyi (open > close) — close vaxtı ertəsi gün sayılır
            // Bu scheduler pəncərə ilə işlədiyi üçün hər iki vaxt üçün ayrıca yoxlama kifayətdir.
            bool shouldOpen = InWindow(nowMinutes, openM.Value);
            bool shouldClose = InWindow(nowMinutes, closeM.Value);

            if (!shouldOpen && !shouldClose) continue;

            // actor user seç (admin və ya permission 20)
            var users = await db.Users
                .AsNoTracking()
                .Include(u => u.Role)
                .Where(u => u.CompanyId == c.Id)
                .Select(u => new
                {
                    u.Id,
                    IsAdmin = u.Role != null && u.Role.IsAdmin,
                    Perms = u.Role != null ? (u.Role.Permissions ?? new List<int>()) : new List<int>()
                })
                .ToListAsync(ct);

            var actor = PickActorUserId(users.Select(x => (x.Id, x.IsAdmin, x.Perms ?? new List<int>())).ToList());
            if (actor == null)
            {
                _logger.LogWarning("[auto-shift] no actor user (company={CompanyId} {CompanyName})", c.Id, c.NameAz);
                continue;
            }

            // aktiv shift varmı?
            CashShiftGetDto? active = null;
            try
            {
                active = await cashShiftService.GetActiveShiftAsync(c.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[auto-shift] active shift fetch failed (company={CompanyId})", c.Id);
                continue;
            }

            var activeId = active?.Id ?? Guid.Empty;
            var hasActive = activeId != Guid.Empty;

            if (shouldOpen && !hasActive)
            {
                try
                {
                    await cashShiftService.OpenShiftAsync(new CashShiftOpenDto
                    {
                        CompanyId = c.Id,
                        OpenedByUserId = actor.Value,
                        IsAutoSchedule = true
                    });
                }
                catch (Exception ex)
                {
                    var msg = ex.Message ?? "";
                    if (msg.Contains("Artıq açıq", StringComparison.OrdinalIgnoreCase) ||
                        msg.Contains("mövcuddur", StringComparison.OrdinalIgnoreCase))
                    {
                        // idempotent
                        continue;
                    }
                    _logger.LogWarning(ex, "[auto-shift] open failed (company={CompanyId})", c.Id);
                }
            }

            if (shouldClose && hasActive)
            {
                // forceClose=false: busy order olanda bağlamasın
                if (!c.AutoCashShiftForceClose)
                {
                    var busyOrders = await db.OrderHeaders
                        .AsNoTracking()
                        .Include(o => o.OrderDetails)
                        .Where(o => o.CompanyId == c.Id && !o.IsClosed)
                        .AnyAsync(o => o.OrderDetails.Any(d => d.Quantity > 0.0001), ct);
                    if (busyOrders) continue;
                }

                try
                {
                    await cashShiftService.CloseShiftAsync(new CashShiftCloseDto
                    {
                        Id = activeId,
                        ClosedByUserId = actor.Value,
                        IsAutoSchedule = true
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[auto-shift] close failed (company={CompanyId})", c.Id);
                }
            }
        }
    }
}

