using System.Security.Cryptography;
using AutoMapper;
using BusinessLayer.Helpers;
using BusinessLayer.DTOs.Audit;
using BusinessLayer.DTOs.CashShift;
using BusinessLayer.Services.Abstractions;
using DAL.Server.Context;
using Domain.Common.Entities;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessLayer.Services.Implementations;

public class CashShiftService : ICashShiftService
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;
    private readonly IAuditLogService _auditLogService;

    private DateTime AzTime => DateTime.UtcNow.AddHours(4);

    public CashShiftService(AppDbContext context, IMapper mapper, IAuditLogService auditLogService)
    {
        _context = context;
        _mapper = mapper;
        _auditLogService = auditLogService;
    }

    private static string GenerateWaiterAccessCode()
    {
        return (RandomNumberGenerator.GetInt32(0, 1_000_000)).ToString("D6");
    }

    public async Task<CashShiftGetDto> GetActiveShiftAsync(Guid companyId)
    {
        var shift = await _context.CashShifts
            .Include(s => s.OpenedByUser)
            .FirstOrDefaultAsync(s => s.CompanyId == companyId && !s.IsClosed);

        if (shift != null && string.IsNullOrWhiteSpace(shift.WaiterAccessCode))
        {
            shift.WaiterAccessCode = GenerateWaiterAccessCode();
            shift.LastModifiedAt = AzTime;
            shift.LastModifiedBy = "system-waiter-code";
            await _context.SaveChangesAsync();
        }

        return _mapper.Map<CashShiftGetDto>(shift);
    }

    public async Task OpenShiftAsync(CashShiftOpenDto dto)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == dto.OpenedByUserId);

        if (user == null) throw new Exception("İstifadəçi tapılmadı!");

        var permissions = user.Role?.Permissions ?? new List<int>();

        bool hasPermission = user.Role!.IsAdmin || permissions.Any(p => p == 20);

        if (!hasPermission)
        {
            throw new Exception($"Səlahiyyətiniz yoxdur! (İcazələr: {string.Join(",", permissions)})");
        }

        var hasActiveShift = await _context.CashShifts
            .AnyAsync(s => s.CompanyId == dto.CompanyId && !s.IsClosed);

        if (hasActiveShift) throw new Exception("Artıq açıq bir növbə mövcuddur!");

        var shift = _mapper.Map<CashShift>(dto);
        var currentTime = AzTime;
        shift.StartTime = currentTime;
        shift.CreatedAt = currentTime;
        shift.CreatedBy = user.Username;
        shift.WaiterAccessCode = GenerateWaiterAccessCode();

        if (dto.IsAutoSchedule)
        {
            shift.OpeningDepositAmount = 0;
        }
        else
        {
            var promptDeposit = await _context.Companies.AsNoTracking()
                .Where(c => c.Id == dto.CompanyId)
                .Select(c => c.CashShiftPromptOpeningDeposit)
                .FirstOrDefaultAsync();
            if (promptDeposit)
            {
                if (dto.OpeningDepositAmount < 0)
                    throw new Exception("Depozit mənfi ola bilməz.");
                shift.OpeningDepositAmount = dto.OpeningDepositAmount;
            }
            else
            {
                shift.OpeningDepositAmount = 0;
            }
        }

        await _context.CashShifts.AddAsync(shift);
        await _context.SaveChangesAsync();

        if (dto.IsAutoSchedule)
        {
            await _auditLogService.LogActionAsync(new AuditLogPostDto
            {
                UserId = dto.OpenedByUserId,
                UserName = user.Username,
                Action = "NÖVBƏ · AVTOMATİK AÇILDI",
                TableName = "—",
                HallName = null,
                Description = $"Cədvəl üzrə açıldı. Vaxt: {currentTime:yyyy-MM-dd HH:mm} (Bakı).",
                CompanyId = dto.CompanyId,
                CreatedBy = user.Username
            });
        }
        else
        {
            await _auditLogService.LogActionAsync(new AuditLogPostDto
            {
                UserId = dto.OpenedByUserId,
                UserName = user.Username,
                Action = "NÖVBƏ AÇILDI",
                TableName = "—",
                HallName = null,
                Description = $"Kassa növbəsi açıldı. Vaxt: {currentTime:yyyy-MM-dd HH:mm} (Bakı).",
                CompanyId = dto.CompanyId,
                CreatedBy = user.Username
            });
        }
    }

    public async Task<CashShiftGetDto?> RegenerateWaiterCodeAsync(Guid companyId)
    {
        var shift = await _context.CashShifts
            .Include(s => s.OpenedByUser)
            .FirstOrDefaultAsync(s => s.CompanyId == companyId && !s.IsClosed);

        if (shift == null) return null;

        shift.WaiterAccessCode = GenerateWaiterAccessCode();
        shift.LastModifiedAt = AzTime;
        shift.LastModifiedBy = "waiter-code-regenerate";
        await _context.SaveChangesAsync();

        return _mapper.Map<CashShiftGetDto>(shift);
    }

    public async Task CloseShiftAsync(CashShiftCloseDto dto)
    {
        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == dto.ClosedByUserId);

        if (user == null) throw new Exception("İstifadəçi tapılmadı!");

        // 1. Növbəni tap
        var shift = await _context.CashShifts
            .FirstOrDefaultAsync(s => s.Id == dto.Id && !s.IsClosed);

        if (shift == null) throw new Exception("Növbə tapılmadı və ya artıq bağlanıb!");

        var realActiveOrders = await _context.OrderHeaders
            .Where(o => o.CompanyId == shift.CompanyId && !o.IsClosed)
            .Include(o => o.OrderDetails)
            .ToListAsync();
        var busyOrders = realActiveOrders.Where(o => o.OrderDetails.Any(d => d.Quantity > 0)).ToList();

        if (busyOrders.Any())
        {
            if (dto.IsAutoSchedule)
            {
                var force = await _context.Companies
                    .AsNoTracking()
                    .Where(c => c.Id == shift.CompanyId)
                    .Select(c => c.AutoCashShiftForceClose)
                    .FirstOrDefaultAsync();

                if (!force)
                {
                    var tableNames = string.Join(", ", busyOrders.Select(o => o.CheckNumber));
                    throw new Exception($"Növbəni bağlamaq olmaz! Bu sifarişlər hələ açıqdır: {tableNames}");
                }
            }
            else if (!dto.AllowCloseWithOpenTables)
            {
                var tableNames = string.Join(", ", busyOrders.Select(o => o.CheckNumber));
                throw new Exception($"Növbəni bağlamaq olmaz! Bu sifarişlər hələ açıqdır: {tableNames}");
            }
        }

        var ghostOrders = realActiveOrders.Where(o => !o.OrderDetails.Any(d => d.Quantity > 0)).ToList();
        foreach (var order in ghostOrders)
        {
            order.IsClosed = true;
            order.CloseTime = AzTime;
            order.CashShiftId = shift.Id;
            order.Note = (order.Note ?? "") + " [SİSTEM: BOŞ OLDUĞU ÜÇÜN QAPADILDI]";
        }

        var busyTableIds = busyOrders.Select(o => o.TableId).Distinct().ToHashSet();

        var tablesToReset = await _context.Tables
            .Where(t => t.CompanyId == shift.CompanyId &&
                        t.Status != Domain.Enums.TableStatus.Empty &&
                        !busyTableIds.Contains(t.Id))
            .ToListAsync();

        foreach (var table in tablesToReset)
        {
            table.Status = Domain.Enums.TableStatus.Empty;
        }

        shift.EndTime = AzTime;
        shift.ClosedByUserId = dto.ClosedByUserId;
        shift.IsClosed = true;
        shift.LastModifiedAt = AzTime;
        shift.LastModifiedBy = user.Username;

        await _context.SaveChangesAsync();

        if (dto.IsAutoSchedule)
        {
            await _auditLogService.LogActionAsync(new AuditLogPostDto
            {
                UserId = dto.ClosedByUserId,
                UserName = user.Username,
                Action = "NÖVBƏ · AVTOMATİK BAĞLANDI",
                TableName = "—",
                HallName = null,
                Description = $"Cədvəl üzrə bağlandı. Vaxt: {AzTime:yyyy-MM-dd HH:mm} (Bakı).",
                CompanyId = shift.CompanyId,
                CreatedBy = user.Username
            });
        }
        else
        {
            await _auditLogService.LogActionAsync(new AuditLogPostDto
            {
                UserId = dto.ClosedByUserId,
                UserName = user.Username,
                Action = "NÖVBƏ BAĞLANDI",
                TableName = "—",
                HallName = null,
                Description = $"Kassa növbəsi bağlandı. Vaxt: {AzTime:yyyy-MM-dd HH:mm} (Bakı).",
                CompanyId = shift.CompanyId,
                CreatedBy = user.Username
            });
        }
    }


    public async Task<object> GetShiftHistoryAsync(Guid companyId, int page = 1, int pageSize = 10)
    {
        var totalCount = await _context.CashShifts.Where(s => s.CompanyId == companyId).CountAsync();

        var shifts = await _context.CashShifts
            .Include(s => s.OpenedByUser)
            .Where(s => s.CompanyId == companyId)
            .OrderByDescending(s => s.StartTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var allClosedOrders = await _context.OrderHeaders
            .Where(o => o.CompanyId == companyId && o.IsClosed)
            .ToListAsync();

        var resultList = new List<CashShiftGetDto>();

        foreach (var shift in shifts)
        {
            var dto = _mapper.Map<CashShiftGetDto>(shift);

            DateTime shiftStart = DateTime.SpecifyKind(shift.StartTime, DateTimeKind.Unspecified);
            DateTime shiftEnd = shift.EndTime.HasValue
                ? DateTime.SpecifyKind(shift.EndTime.Value, DateTimeKind.Unspecified)
                : DateTime.MaxValue;

            var shiftId = shift.Id;
            var shiftOrders = allClosedOrders.Where(o =>
            {
                if (!o.CloseTime.HasValue) return false;
                if (o.CashShiftId == shiftId) return true;
                if (o.CashShiftId != null) return false;
                DateTime orderTime = DateTime.SpecifyKind(o.CloseTime.Value, DateTimeKind.Unspecified);
                return orderTime >= shiftStart.AddSeconds(-5) && orderTime <= shiftEnd.AddSeconds(5);
            }).ToList();

            var shiftRevenue = shiftOrders.Sum(x => x.TotalAmount);
            var shiftCashRaw = shiftOrders.Sum(x =>
                OrderPaymentNet.NaqdKartReportGross(x.TotalAmount, x.BehAmount, x.ServiceAmount, x.PaidCash, x.PaidCard, x.CustomPaymentMethodId).Cash);
            var shiftCardRaw = shiftOrders.Sum(x =>
                OrderPaymentNet.NaqdKartReportGross(x.TotalAmount, x.BehAmount, x.ServiceAmount, x.PaidCash, x.PaidCard, x.CustomPaymentMethodId).Card);
            var shiftCustomSum = shiftOrders
                .Where(x => x.CustomPaymentMethodId.HasValue)
                .Sum(x => OrderPaymentNet.PayableAmount(x.TotalAmount, x.BehAmount));
            var (shiftCash, shiftCard) = OrderPaymentNet.ReconcileReportPaymentTotals(
                shiftRevenue, shiftCashRaw, shiftCardRaw, shiftCustomSum);
            dto.TotalCash = shiftCash;
            dto.TotalCard = shiftCard;
            dto.TotalRevenue = shiftRevenue;
            dto.OrderCount = shiftOrders.Count;

            dto.OpenedByUserName = shift.OpenedByUser?.FullName ?? "Admin";
            resultList.Add(dto);
        }

        return new { Items = resultList, TotalCount = totalCount };
    }

    public async Task<object> GetActiveShiftOrdersAsync(Guid companyId, int page = 1, int pageSize = 10)
    {
        var activeShift = await _context.CashShifts
            .FirstOrDefaultAsync(s => s.CompanyId == companyId && !s.IsClosed);

        if (activeShift == null)
        {
            return new { Orders = new List<object>(), Stats = new { }, TotalPages = 0, CurrentPage = page };
        }

        var activeId = activeShift.Id;
        var shiftStart = activeShift.StartTime;

        // Açıq çeklər + bu növbəyə düşən bağlı çeklər (CashShiftId və ya köhnə CloseTime pəncərəsi).
        var query = _context.OrderHeaders
            .Include(o => o.Table).ThenInclude(t => t.Hall)
            .Include(o => o.OrderDetails)
            .Include(o => o.CustomPaymentMethod)
            .Where(o => o.CompanyId == companyId &&
                        (!o.IsClosed
                         || (o.IsClosed &&
                             (o.CashShiftId == activeId
                              || (o.CashShiftId == null && o.CloseTime.HasValue && o.CloseTime.Value >= shiftStart)))));

        var statsData = await query.Select(o => new
        {
            o.TotalAmount,
            o.BehAmount,
            o.ServiceAmount,
            o.PaidCash,
            o.PaidCard,
            o.CustomPaymentMethodId,
            o.IsClosed
        }).ToListAsync();

        var closedStats = statsData.Where(x => x.IsClosed).ToList();
        var openStats = statsData.Where(x => !x.IsClosed).ToList();

        var stats = new
        {
            totalCash = closedStats.Sum(x =>
                OrderPaymentNet.NaqdKartReportGross(x.TotalAmount, x.BehAmount, x.ServiceAmount, x.PaidCash, x.PaidCard, x.CustomPaymentMethodId).Cash),
            totalCard = closedStats.Sum(x =>
                OrderPaymentNet.NaqdKartReportGross(x.TotalAmount, x.BehAmount, x.ServiceAmount, x.PaidCash, x.PaidCard, x.CustomPaymentMethodId).Card),
            totalAll = statsData.Sum(x => x.TotalAmount),
            totalActive = openStats.Sum(x => x.TotalAmount),
            allCount = statsData.Count,
            openingDepositAmount = activeShift.OpeningDepositAmount
        };

        var orders = await query
            .OrderBy(o => o.IsClosed)
            .ThenByDescending(o => o.CloseTime ?? o.OpenTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync();

        return new
        {
            Orders = _mapper.Map<List<BusinessLayer.DTOs.OrderHeader.OrderHeaderGetDto>>(orders),
            Stats = stats,
            TotalPages = (int)Math.Ceiling((double)stats.allCount / pageSize),
            CurrentPage = page
        };
    }
}