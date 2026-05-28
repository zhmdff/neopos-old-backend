using BusinessLayer.DTOs.Reports;
using BusinessLayer.Helpers;
using BusinessLayer.Services.Abstractions;
using DAL.Server.Context;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace BusinessLayer.Services.Implementations;

public class ReportService : IReportService
{
    private readonly AppDbContext _context;

    public ReportService(AppDbContext context) => _context = context;

    // Max(məhsul, depozit) ilə uyğun: yalnız depozit > məhsul olduqda fərq; əks halda 0.
    private static decimal DepositRevenuePortion(decimal depositAmount, decimal foodTotalFromLines) =>
        depositAmount <= 0 ? 0 : Math.Max(0, depositAmount - foodTotalFromLines);

    private static decimal NormalizedPaidTotal(decimal totalAmount, decimal behAmount, decimal paidCash, decimal paidCard)
    {
        var n = OrderPaymentNet.NormalizePaid(totalAmount, behAmount, paidCash, paidCard);
        return n.Cash + n.Card;
    }

    private static List<CustomPaymentTotalDto> BuildCustomPaymentTotalsFromRows(
        List<(Guid Id, string Name, decimal Tot, decimal Beh, decimal Cash, decimal Card)> rows)
    {
        if (rows.Count == 0) return new List<CustomPaymentTotalDto>();
        return rows
            .GroupBy(r => r.Id)
            .Select(g => new CustomPaymentTotalDto
            {
                MethodId = g.Key,
                MethodName = g.Select(x => x.Name).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? "Xüsusi",
                Amount = g.Sum(x => NormalizedPaidTotal(x.Tot, x.Beh, x.Cash, x.Card)),
                OrderCount = g.Count()
            })
            .OrderByDescending(x => x.Amount)
            .ToList();
    }

    public async Task<SummaryReportDto> GetGeneralSummaryAsync(
        DateTime start,
        DateTime end,
        Guid companyId,
        bool includeOpenTables = false,
        DateTime? openOrdersOpenedOnOrAfter = null,
        Guid? cashShiftAttributionId = null)
    {
        // 1. Datada saatlar onsuz da düzdürsə, bir daha AddHours(4) etmirik.
        // Sadəcə filtr üçün daxil edilən start/end vaxtlarını istifadə edirik.
        // cashShiftAttributionId: yalnız bu növbəyə yazılmış bağlı çeklər (cari növbə hesabatları üçün dəqiq filtr).
        var closed = await _context.OrderHeaders
            .Include(o => o.OrderDetails)
            .ThenInclude(d => d.Product)
            .Where(o => o.IsClosed && o.CompanyId == companyId && o.CloseTime.HasValue)
            .Where(o => cashShiftAttributionId == null
                ? (o.CloseTime!.Value >= start && o.CloseTime.Value <= end)
                : o.CashShiftId == cashShiftAttributionId)
            .Select(o => new
            {
                o.TotalAmount,
                o.BehAmount,
                o.ServiceAmount,
                o.DepositAmount,
                o.DiscountAmount,
                FoodTotal = o.OrderDetails.Sum(d => d.TotalPrice),
                o.PaidCash,
                o.PaidCard,
                o.CloseTime,
                o.CustomPaymentMethodId,
                CustomMethodName = o.CustomPaymentMethod != null ? o.CustomPaymentMethod.NameAz : null,
                TotalCost = o.OrderDetails.Sum(d => (decimal)d.Quantity * (d.Product != null ? d.Product.CostPrice : 0))
            })
            .ToListAsync();

        var closedRevenue = closed.Sum(x => x.TotalAmount);
        var closedDiscount = closed.Sum(x => x.DiscountAmount);
        var closedCost = closed.Sum(x => x.TotalCost);
        var closedCash = closed.Sum(x =>
            OrderPaymentNet.NaqdKartReportGross(x.TotalAmount, x.BehAmount, x.ServiceAmount, x.PaidCash, x.PaidCard, x.CustomPaymentMethodId).Cash);
        var closedCard = closed.Sum(x =>
            OrderPaymentNet.NaqdKartReportGross(x.TotalAmount, x.BehAmount, x.ServiceAmount, x.PaidCash, x.PaidCard, x.CustomPaymentMethodId).Card);
        var closedService = closed.Sum(x => x.ServiceAmount);
        var closedDeposit = closed.Sum(x => DepositRevenuePortion(x.DepositAmount, x.FoodTotal));

        decimal openRev = 0, openCost = 0, openCash = 0, openCard = 0;
        decimal openService = 0, openDeposit = 0;
        decimal openDiscount = 0m;
        var openCnt = 0;
        var customRows = closed
            .Where(o => o.CustomPaymentMethodId.HasValue)
            .Select(o => (
                Id: o.CustomPaymentMethodId!.Value,
                Name: o.CustomMethodName ?? "",
                Tot: o.TotalAmount,
                Beh: o.BehAmount,
                Cash: o.PaidCash,
                Card: o.PaidCard))
            .ToList();
        if (includeOpenTables)
        {
            var openQ = _context.OrderHeaders
                .Include(o => o.OrderDetails)
                .ThenInclude(d => d.Product)
                .Where(o => !o.IsClosed && o.CompanyId == companyId);
            if (cashShiftAttributionId.HasValue)
            {
                var sid = cashShiftAttributionId.Value;
                var t0 = openOrdersOpenedOnOrAfter ?? start;
                openQ = openQ.Where(o =>
                    o.CashShiftId == sid ||
                    (o.CashShiftId == null && o.OpenTime >= t0));
            }
            else if (openOrdersOpenedOnOrAfter.HasValue)
            {
                var t0 = openOrdersOpenedOnOrAfter.Value;
                openQ = openQ.Where(o => o.OpenTime >= t0);
            }

            var openOrders = await openQ
                .Select(o => new
                {
                    o.TotalAmount,
                    o.BehAmount,
                    o.ServiceAmount,
                    o.DepositAmount,
                    o.DiscountAmount,
                    FoodTotal = o.OrderDetails.Sum(d => d.TotalPrice),
                    o.PaidCash,
                    o.PaidCard,
                    o.CustomPaymentMethodId,
                    CustomMethodName = o.CustomPaymentMethod != null ? o.CustomPaymentMethod.NameAz : null,
                    TotalCost = o.OrderDetails.Sum(d => (decimal)d.Quantity * (d.Product != null ? d.Product.CostPrice : 0))
                })
                .ToListAsync();

            openRev = openOrders.Sum(x => x.TotalAmount);
            openCost = openOrders.Sum(x => x.TotalCost);
            openCash = openOrders.Sum(x =>
                OrderPaymentNet.NaqdKartReportGross(x.TotalAmount, x.BehAmount, x.ServiceAmount, x.PaidCash, x.PaidCard, x.CustomPaymentMethodId).Cash);
            openCard = openOrders.Sum(x =>
                OrderPaymentNet.NaqdKartReportGross(x.TotalAmount, x.BehAmount, x.ServiceAmount, x.PaidCash, x.PaidCard, x.CustomPaymentMethodId).Card);
            openService = openOrders.Sum(x => x.ServiceAmount);
            openDeposit = openOrders.Sum(x => DepositRevenuePortion(x.DepositAmount, x.FoodTotal));
            openCnt = openOrders.Count;
            openDiscount = openOrders.Sum(x => x.DiscountAmount);

            customRows.AddRange(openOrders
                .Where(o => o.CustomPaymentMethodId.HasValue)
                .Select(o => (
                    Id: o.CustomPaymentMethodId!.Value,
                    Name: o.CustomMethodName ?? "",
                    Tot: o.TotalAmount,
                    Beh: o.BehAmount,
                    Cash: o.PaidCash,
                    Card: o.PaidCard)));
        }

        var dailyData = closed
            .GroupBy(x => x.CloseTime!.Value.Date)
            .Select(g => new DailyReportItemDto
            {
                Date = g.Key,
                TotalRevenue = g.Sum(x => x.TotalAmount)
            })
            .OrderBy(x => x.Date)
            .ToList();

        var customPaymentTotals = BuildCustomPaymentTotalsFromRows(customRows);

        var totalRevenue = closedRevenue + openRev;
        var totalCashRaw = closedCash + openCash;
        var totalCardRaw = closedCard + openCard;
        var customPaidSum = customPaymentTotals.Sum(x => x.Amount);
        var (totalCash, totalCard) = OrderPaymentNet.ReconcileReportPaymentTotals(
            totalRevenue, totalCashRaw, totalCardRaw, customPaidSum);

        return new SummaryReportDto
        {
            TotalRevenue = totalRevenue,
            TotalCost = closedCost + openCost,
            TotalCash = totalCash,
            TotalCard = totalCard,
            CustomPaymentTotals = customPaymentTotals,
            OrderCount = closed.Count,
            ClosedRevenue = closedRevenue,
            ClosedOrderCount = closed.Count,
            OpenTablesIncluded = includeOpenTables,
            OpenRevenueAdded = openRev,
            OpenCostAdded = openCost,
            OpenCashAdded = openCash,
            OpenCardAdded = openCard,
            OpenOrderCount = openCnt,
            ServiceFeeRevenue = closedService + openService,
            DepositRevenue = closedDeposit + openDeposit,
            TotalDiscountAmount = closedDiscount + openDiscount,
            DailyReports = dailyData
        };
    }

    private static string CleanKey(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        return s.Trim();
    }

    public async Task<BreakdownReportDto<TableBreakdownItemDto>> GetTableBreakdownAsync(
        DateTime start,
        DateTime end,
        Guid companyId,
        bool includeOpenTables = false,
        DateTime? openOrdersOpenedOnOrAfter = null,
        Guid? cashShiftAttributionId = null)
    {
        var closed = await _context.OrderHeaders
            .AsNoTracking()
            .Include(o => o.Table)
            .ThenInclude(t => t.Hall)
            .Where(o => o.IsClosed && o.CompanyId == companyId && o.CloseTime.HasValue)
            .Where(o => cashShiftAttributionId == null
                ? (o.CloseTime!.Value >= start && o.CloseTime.Value <= end)
                : o.CashShiftId == cashShiftAttributionId)
            .Select(o => new
            {
                o.TableId,
                TableName = o.Table.NameAz,
                HallName = o.Table.Hall.NameAz,
                o.TotalAmount,
                o.BehAmount,
                o.ServiceAmount,
                o.PaidCash,
                o.PaidCard,
                o.CustomPaymentMethodId
            })
            .ToListAsync();

        var byTable = closed
            .GroupBy(x => new { x.TableId, x.TableName, x.HallName })
            .Select(g => new TableBreakdownItemDto
            {
                TableId = g.Key.TableId,
                TableName = g.Key.TableName ?? "",
                HallName = g.Key.HallName ?? "",
                Revenue = g.Sum(x => x.TotalAmount),
                Cash = g.Sum(x => OrderPaymentNet.NaqdKartReportGross(x.TotalAmount, x.BehAmount, x.ServiceAmount, x.PaidCash, x.PaidCard, x.CustomPaymentMethodId).Cash),
                Card = g.Sum(x => OrderPaymentNet.NaqdKartReportGross(x.TotalAmount, x.BehAmount, x.ServiceAmount, x.PaidCash, x.PaidCard, x.CustomPaymentMethodId).Card),
                OrderCount = g.Count(),
                OpenRevenueAdded = 0,
                OpenOrderCount = 0
            })
            .ToDictionary(x => x.TableId, x => x);

        decimal openRev = 0, openCash = 0, openCard = 0;
        var openCnt = 0;

        if (includeOpenTables)
        {
            var openQ = _context.OrderHeaders
                .AsNoTracking()
                .Include(o => o.Table)
                .ThenInclude(t => t.Hall)
                .Where(o => !o.IsClosed && o.CompanyId == companyId);

            if (cashShiftAttributionId.HasValue)
            {
                var sid = cashShiftAttributionId.Value;
                var t0 = openOrdersOpenedOnOrAfter ?? start;
                openQ = openQ.Where(o =>
                    o.CashShiftId == sid ||
                    (o.CashShiftId == null && o.OpenTime >= t0));
            }
            else if (openOrdersOpenedOnOrAfter.HasValue)
            {
                var t0 = openOrdersOpenedOnOrAfter.Value;
                openQ = openQ.Where(o => o.OpenTime >= t0);
            }

            var open = await openQ
                .Select(o => new
                {
                    o.TableId,
                    TableName = o.Table.NameAz,
                    HallName = o.Table.Hall.NameAz,
                    o.TotalAmount,
                    o.BehAmount,
                    o.ServiceAmount,
                    o.PaidCash,
                    o.PaidCard,
                    o.CustomPaymentMethodId
                })
                .ToListAsync();

            openRev = open.Sum(x => x.TotalAmount);
            openCash = open.Sum(x =>
                OrderPaymentNet.NaqdKartReportGross(x.TotalAmount, x.BehAmount, x.ServiceAmount, x.PaidCash, x.PaidCard, x.CustomPaymentMethodId).Cash);
            openCard = open.Sum(x =>
                OrderPaymentNet.NaqdKartReportGross(x.TotalAmount, x.BehAmount, x.ServiceAmount, x.PaidCash, x.PaidCard, x.CustomPaymentMethodId).Card);
            openCnt = open.Count;

            foreach (var g in open.GroupBy(x => new { x.TableId, x.TableName, x.HallName }))
            {
                if (!byTable.TryGetValue(g.Key.TableId, out var dto))
                {
                    dto = new TableBreakdownItemDto
                    {
                        TableId = g.Key.TableId,
                        TableName = g.Key.TableName ?? "",
                        HallName = g.Key.HallName ?? "",
                        Revenue = 0,
                        Cash = 0,
                        Card = 0,
                        OrderCount = 0
                    };
                    byTable[g.Key.TableId] = dto;
                }
                dto.OpenRevenueAdded = g.Sum(x => x.TotalAmount);
                dto.OpenOrderCount = g.Count();
                dto.Revenue += dto.OpenRevenueAdded;
                dto.Cash += g.Sum(x =>
                    OrderPaymentNet.NaqdKartReportGross(x.TotalAmount, x.BehAmount, x.ServiceAmount, x.PaidCash, x.PaidCard, x.CustomPaymentMethodId).Cash);
                dto.Card += g.Sum(x =>
                    OrderPaymentNet.NaqdKartReportGross(x.TotalAmount, x.BehAmount, x.ServiceAmount, x.PaidCash, x.PaidCard, x.CustomPaymentMethodId).Card);
                dto.OrderCount += g.Count();
            }
        }

        var items = byTable.Values
            .OrderByDescending(x => x.Revenue)
            .ThenBy(x => x.HallName)
            .ThenBy(x => x.TableName)
            .ToList();

        return new BreakdownReportDto<TableBreakdownItemDto>
        {
            Start = start,
            End = end,
            OpenTablesIncluded = includeOpenTables,
            TotalRevenue = items.Sum(x => x.Revenue),
            TotalCash = items.Sum(x => x.Cash),
            TotalCard = items.Sum(x => x.Card),
            OrderCount = items.Sum(x => x.OrderCount),
            OpenRevenueAdded = openRev,
            OpenCashAdded = openCash,
            OpenCardAdded = openCard,
            OpenOrderCount = openCnt,
            Items = items
        };
    }

    public async Task<BreakdownReportDto<WaiterBreakdownItemDto>> GetWaiterBreakdownAsync(
        DateTime start,
        DateTime end,
        Guid companyId,
        bool includeOpenTables = false,
        DateTime? openOrdersOpenedOnOrAfter = null,
        Guid? cashShiftAttributionId = null)
    {
        var closed = await _context.OrderHeaders
            .AsNoTracking()
            .Where(o => o.IsClosed && o.CompanyId == companyId && o.CloseTime.HasValue)
            .Where(o => cashShiftAttributionId == null
                ? (o.CloseTime!.Value >= start && o.CloseTime.Value <= end)
                : o.CashShiftId == cashShiftAttributionId)
            .Select(o => new
            {
                WaiterName = o.WaiterName,
                CashierName = o.CashierName,
                o.TotalAmount,
                o.BehAmount,
                o.ServiceAmount,
                o.PaidCash,
                o.PaidCard,
                o.CustomPaymentMethodId
            })
            .ToListAsync();

        var byWaiter = closed
            .GroupBy(x => CleanKey(x.WaiterName) != "" ? CleanKey(x.WaiterName) : CleanKey(x.CashierName))
            .Select(g => new WaiterBreakdownItemDto
            {
                WaiterName = string.IsNullOrWhiteSpace(g.Key) ? "—" : g.Key,
                Revenue = g.Sum(x => x.TotalAmount),
                Cash = g.Sum(x => OrderPaymentNet.NaqdKartReportGross(x.TotalAmount, x.BehAmount, x.ServiceAmount, x.PaidCash, x.PaidCard, x.CustomPaymentMethodId).Cash),
                Card = g.Sum(x => OrderPaymentNet.NaqdKartReportGross(x.TotalAmount, x.BehAmount, x.ServiceAmount, x.PaidCash, x.PaidCard, x.CustomPaymentMethodId).Card),
                OrderCount = g.Count(),
                OpenRevenueAdded = 0,
                OpenOrderCount = 0
            })
            .ToDictionary(x => x.WaiterName, x => x, StringComparer.InvariantCultureIgnoreCase);

        decimal openRev = 0, openCash = 0, openCard = 0;
        var openCnt = 0;

        if (includeOpenTables)
        {
            var openQ = _context.OrderHeaders
                .AsNoTracking()
                .Where(o => !o.IsClosed && o.CompanyId == companyId);

            if (cashShiftAttributionId.HasValue)
            {
                var sid = cashShiftAttributionId.Value;
                var t0 = openOrdersOpenedOnOrAfter ?? start;
                openQ = openQ.Where(o =>
                    o.CashShiftId == sid ||
                    (o.CashShiftId == null && o.OpenTime >= t0));
            }
            else if (openOrdersOpenedOnOrAfter.HasValue)
            {
                var t0 = openOrdersOpenedOnOrAfter.Value;
                openQ = openQ.Where(o => o.OpenTime >= t0);
            }

            var open = await openQ
                .Select(o => new
                {
                    WaiterName = o.WaiterName,
                    CashierName = o.CashierName,
                    o.TotalAmount,
                    o.BehAmount,
                    o.ServiceAmount,
                    o.PaidCash,
                    o.PaidCard,
                    o.CustomPaymentMethodId
                })
                .ToListAsync();

            openRev = open.Sum(x => x.TotalAmount);
            openCash = open.Sum(x =>
                OrderPaymentNet.NaqdKartReportGross(x.TotalAmount, x.BehAmount, x.ServiceAmount, x.PaidCash, x.PaidCard, x.CustomPaymentMethodId).Cash);
            openCard = open.Sum(x =>
                OrderPaymentNet.NaqdKartReportGross(x.TotalAmount, x.BehAmount, x.ServiceAmount, x.PaidCash, x.PaidCard, x.CustomPaymentMethodId).Card);
            openCnt = open.Count;

            foreach (var g in open.GroupBy(x => CleanKey(x.WaiterName) != "" ? CleanKey(x.WaiterName) : CleanKey(x.CashierName)))
            {
                var key = string.IsNullOrWhiteSpace(g.Key) ? "—" : g.Key;
                if (!byWaiter.TryGetValue(key, out var dto))
                {
                    dto = new WaiterBreakdownItemDto { WaiterName = key };
                    byWaiter[key] = dto;
                }
                dto.OpenRevenueAdded = g.Sum(x => x.TotalAmount);
                dto.OpenOrderCount = g.Count();
                dto.Revenue += dto.OpenRevenueAdded;
                dto.Cash += g.Sum(x =>
                    OrderPaymentNet.NaqdKartReportGross(x.TotalAmount, x.BehAmount, x.ServiceAmount, x.PaidCash, x.PaidCard, x.CustomPaymentMethodId).Cash);
                dto.Card += g.Sum(x =>
                    OrderPaymentNet.NaqdKartReportGross(x.TotalAmount, x.BehAmount, x.ServiceAmount, x.PaidCash, x.PaidCard, x.CustomPaymentMethodId).Card);
                dto.OrderCount += g.Count();
            }
        }

        var items = byWaiter.Values
            .OrderByDescending(x => x.Revenue)
            .ThenByDescending(x => x.OrderCount)
            .ToList();

        return new BreakdownReportDto<WaiterBreakdownItemDto>
        {
            Start = start,
            End = end,
            OpenTablesIncluded = includeOpenTables,
            TotalRevenue = items.Sum(x => x.Revenue),
            TotalCash = items.Sum(x => x.Cash),
            TotalCard = items.Sum(x => x.Card),
            OrderCount = items.Sum(x => x.OrderCount),
            OpenRevenueAdded = openRev,
            OpenCashAdded = openCash,
            OpenCardAdded = openCard,
            OpenOrderCount = openCnt,
            Items = items
        };
    }

    public async Task<BreakdownReportDto<WorkshopBreakdownItemDto>> GetWorkshopBreakdownAsync(
        DateTime start,
        DateTime end,
        Guid companyId,
        bool includeOpenTables = false,
        DateTime? openOrdersOpenedOnOrAfter = null,
        Guid? cashShiftAttributionId = null)
    {
        var closedDetails = await _context.OrderDetails
            .AsNoTracking()
            .Include(d => d.Product)
            .ThenInclude(p => p.Workshop)
            .Include(d => d.OrderHeader)
            .Where(d => d.CompanyId == companyId)
            .Where(d => d.Product != null)
            .Where(d => d.OrderHeader.IsClosed && d.OrderHeader.CloseTime.HasValue)
            .Where(d => cashShiftAttributionId == null
                ? (d.OrderHeader.CloseTime!.Value >= start && d.OrderHeader.CloseTime.Value <= end)
                : d.OrderHeader.CashShiftId == cashShiftAttributionId)
            .Select(d => new
            {
                WorkshopId = d.Product!.WorkshopId,
                WorkshopName = d.Product.Workshop != null ? d.Product.Workshop.NameAz : "",
                Revenue = d.TotalPrice
            })
            .ToListAsync();

        decimal openRevenueAdded = 0;
        var openOrderCount = 0;

        if (includeOpenTables)
        {
            var openQ = _context.OrderDetails
                .AsNoTracking()
                .Include(d => d.Product)
                .ThenInclude(p => p.Workshop)
                .Include(d => d.OrderHeader)
                .Where(d => d.CompanyId == companyId)
                .Where(d => d.Product != null)
                .Where(d => !d.OrderHeader.IsClosed);

            if (cashShiftAttributionId.HasValue)
            {
                var sid = cashShiftAttributionId.Value;
                var t0 = openOrdersOpenedOnOrAfter ?? start;
                openQ = openQ.Where(d =>
                    d.OrderHeader.CashShiftId == sid ||
                    (d.OrderHeader.CashShiftId == null && d.OrderHeader.OpenTime >= t0));
            }
            else if (openOrdersOpenedOnOrAfter.HasValue)
            {
                var t0 = openOrdersOpenedOnOrAfter.Value;
                openQ = openQ.Where(d => d.OrderHeader.OpenTime >= t0);
            }

            var openDetails = await openQ
                .Select(d => new
                {
                    d.OrderHeaderId,
                    WorkshopId = d.Product!.WorkshopId,
                    WorkshopName = d.Product.Workshop != null ? d.Product.Workshop.NameAz : "",
                    Revenue = d.TotalPrice
                })
                .ToListAsync();

            openRevenueAdded = openDetails.Sum(x => x.Revenue);
            openOrderCount = openDetails.Select(x => x.OrderHeaderId).Distinct().Count();

            closedDetails.AddRange(openDetails.Select(x => new
            {
                x.WorkshopId,
                x.WorkshopName,
                x.Revenue
            }));
        }

        var items = closedDetails
            .GroupBy(x => new { x.WorkshopId, NameKey = CleanKey(x.WorkshopName) })
            .Select(g => new WorkshopBreakdownItemDto
            {
                WorkshopId = g.Key.WorkshopId,
                WorkshopName = string.IsNullOrEmpty(g.Key.NameKey) ? "—" : g.Key.NameKey,
                Revenue = g.Sum(x => x.Revenue)
            })
            .OrderByDescending(x => x.Revenue)
            .ToList();

        var totalRevenue = items.Sum(x => x.Revenue);

        return new BreakdownReportDto<WorkshopBreakdownItemDto>
        {
            Start = start,
            End = end,
            OpenTablesIncluded = includeOpenTables,
            TotalRevenue = totalRevenue,
            TotalCash = 0,
            TotalCard = 0,
            OrderCount = 0,
            OpenRevenueAdded = openRevenueAdded,
            OpenCashAdded = 0,
            OpenCardAdded = 0,
            OpenOrderCount = openOrderCount,
            Items = items
        };
    }

    public async Task<BreakdownReportDto<ProductBreakdownItemDto>> GetProductBreakdownAsync(
        DateTime start,
        DateTime end,
        Guid companyId,
        int take = 50,
        bool includeOpenTables = false,
        DateTime? openOrdersOpenedOnOrAfter = null,
        Guid? cashShiftAttributionId = null)
    {
        var safeTake = Math.Max(1, Math.Min(take, 500));

        var closedDetails = await _context.OrderDetails
            .AsNoTracking()
            .Include(d => d.Product)
            .ThenInclude(p => p.Category)
            .Include(d => d.OrderHeader)
            .Where(d => d.CompanyId == companyId)
            .Where(d => d.OrderHeader.IsClosed && d.OrderHeader.CloseTime.HasValue)
            .Where(d => cashShiftAttributionId == null
                ? (d.OrderHeader.CloseTime!.Value >= start && d.OrderHeader.CloseTime.Value <= end)
                : d.OrderHeader.CashShiftId == cashShiftAttributionId)
            .Select(d => new
            {
                d.ProductId,
                ProductName = d.Product != null ? d.Product.NameAz : d.ProductName,
                CategoryName = d.Product != null && d.Product.Category != null ? d.Product.Category.NameAz : null,
                d.ProductVariantId,
                d.ProductVariantName,
                Qty = (decimal)d.Quantity,
                Revenue = d.TotalPrice,
                Cost = ((d.Product != null ? d.Product.CostPrice : 0) * (decimal)d.Quantity)
            })
            .ToListAsync();

        decimal openRevenueAdded = 0;
        var openOrderCount = 0;

        if (includeOpenTables)
        {
            var openQ = _context.OrderDetails
                .AsNoTracking()
                .Include(d => d.Product)
                .ThenInclude(p => p.Category)
                .Include(d => d.OrderHeader)
                .Where(d => d.CompanyId == companyId)
                .Where(d => !d.OrderHeader.IsClosed);

            if (cashShiftAttributionId.HasValue)
            {
                var sid = cashShiftAttributionId.Value;
                var t0 = openOrdersOpenedOnOrAfter ?? start;
                openQ = openQ.Where(d =>
                    d.OrderHeader.CashShiftId == sid ||
                    (d.OrderHeader.CashShiftId == null && d.OrderHeader.OpenTime >= t0));
            }
            else if (openOrdersOpenedOnOrAfter.HasValue)
            {
                var t0 = openOrdersOpenedOnOrAfter.Value;
                openQ = openQ.Where(d => d.OrderHeader.OpenTime >= t0);
            }

            var openDetails = await openQ
                .Select(d => new
                {
                    d.OrderHeaderId,
                    d.ProductId,
                    ProductName = d.Product != null ? d.Product.NameAz : d.ProductName,
                    CategoryName = d.Product != null && d.Product.Category != null ? d.Product.Category.NameAz : null,
                    d.ProductVariantId,
                    d.ProductVariantName,
                    Qty = (decimal)d.Quantity,
                    Revenue = d.TotalPrice,
                    Cost = ((d.Product != null ? d.Product.CostPrice : 0) * (decimal)d.Quantity)
                })
                .ToListAsync();

            openRevenueAdded = openDetails.Sum(x => x.Revenue);
            openOrderCount = openDetails.Select(x => x.OrderHeaderId).Distinct().Count();

            closedDetails.AddRange(openDetails.Select(x => new
            {
                x.ProductId,
                x.ProductName,
                x.CategoryName,
                x.ProductVariantId,
                x.ProductVariantName,
                x.Qty,
                x.Revenue,
                x.Cost
            }));
        }

        var items = closedDetails
            .GroupBy(x => new
            {
                x.ProductId,
                x.ProductName,
                x.CategoryName,
                x.ProductVariantId,
                VariantNameKey = CleanKey(x.ProductVariantName)
            })
            .Select(g => new ProductBreakdownItemDto
            {
                ProductId = g.Key.ProductId,
                ProductName = CleanKey(g.Key.ProductName),
                CategoryName = CleanKey(g.Key.CategoryName),
                ProductVariantId = g.Key.ProductVariantId,
                ProductVariantName = string.IsNullOrEmpty(g.Key.VariantNameKey) ? null : g.Key.VariantNameKey,
                Quantity = g.Sum(x => x.Qty),
                Revenue = g.Sum(x => x.Revenue),
                Cost = g.Sum(x => x.Cost)
            })
            .OrderByDescending(x => x.Revenue)
            .ThenByDescending(x => x.Quantity)
            .Take(safeTake)
            .ToList();

        var closedRevenue = closedDetails.Sum(x => x.Revenue);

        return new BreakdownReportDto<ProductBreakdownItemDto>
        {
            Start = start,
            End = end,
            OpenTablesIncluded = includeOpenTables,

            TotalRevenue = closedRevenue,
            TotalCash = 0,
            TotalCard = 0,
            OrderCount = 0,

            OpenRevenueAdded = openRevenueAdded,
            OpenCashAdded = 0,
            OpenCardAdded = 0,
            OpenOrderCount = openOrderCount,

            Items = items
        };
    }

    public async Task<BreakdownReportDto<ProductBreakdownItemDto>> GetShiftProductBreakdownAsync(
        Guid shiftId,
        Guid companyId,
        int take = 50,
        bool includeOpenTables = false)
    {
        var shift = await _context.CashShifts
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == shiftId && s.CompanyId == companyId);

        if (shift == null)
            throw new Exception("Növbə tapılmadı!");

        var endTime = shift.EndTime ?? DateTime.UtcNow.AddHours(4);

        return await GetProductBreakdownAsync(
            shift.StartTime,
            endTime,
            companyId,
            take,
            includeOpenTables,
            includeOpenTables ? shift.StartTime : null,
            shift.Id);
    }

    public async Task<BreakdownReportDto<TableBreakdownItemDto>> GetShiftTableBreakdownAsync(
        Guid shiftId,
        Guid companyId,
        bool includeOpenTables = false)
    {
        var shift = await _context.CashShifts
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == shiftId && s.CompanyId == companyId);

        if (shift == null)
            throw new Exception("Növbə tapılmadı!");

        var endTime = shift.EndTime ?? DateTime.UtcNow.AddHours(4);

        return await GetTableBreakdownAsync(
            shift.StartTime,
            endTime,
            companyId,
            includeOpenTables,
            includeOpenTables ? shift.StartTime : null,
            shift.Id);
    }

    public async Task<BreakdownReportDto<WaiterBreakdownItemDto>> GetShiftWaiterBreakdownAsync(
        Guid shiftId,
        Guid companyId,
        bool includeOpenTables = false)
    {
        var shift = await _context.CashShifts
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == shiftId && s.CompanyId == companyId);

        if (shift == null)
            throw new Exception("Növbə tapılmadı!");

        var endTime = shift.EndTime ?? DateTime.UtcNow.AddHours(4);

        return await GetWaiterBreakdownAsync(
            shift.StartTime,
            endTime,
            companyId,
            includeOpenTables,
            includeOpenTables ? shift.StartTime : null,
            shift.Id);
    }

    public async Task<BreakdownReportDto<WorkshopBreakdownItemDto>> GetShiftWorkshopBreakdownAsync(
        Guid shiftId,
        Guid companyId,
        bool includeOpenTables = false)
    {
        var shift = await _context.CashShifts
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == shiftId && s.CompanyId == companyId);

        if (shift == null)
            throw new Exception("Növbə tapılmadı!");

        var endTime = shift.EndTime ?? DateTime.UtcNow.AddHours(4);

        return await GetWorkshopBreakdownAsync(
            shift.StartTime,
            endTime,
            companyId,
            includeOpenTables,
            includeOpenTables ? shift.StartTime : null,
            shift.Id);
    }

    public async Task<CustomerLoyaltyReportDto> GetCustomerLoyaltyAsync(DateTime start, DateTime end, Guid companyId, string? q = null, int take = 200)
    {
        var tq = (q ?? "").Trim();
        var qLower = tq.ToLower();

        var custQuery = _context.Customers
            .AsNoTracking()
            .Where(c => c.CompanyId == companyId && !c.IsDeleted);

        if (!string.IsNullOrWhiteSpace(tq))
        {
            custQuery = custQuery.Where(c =>
                c.FullName.ToLower().Contains(qLower) ||
                c.Phone.Contains(tq));
        }

        // əvvəlcə top müştərilər (son yaradılanlar) — sonra order aggregation ilə dolduracağıq
        var customers = await custQuery
            .OrderByDescending(c => c.CreatedAt)
            .Take(Math.Max(10, Math.Min(take, 1000)))
            .Select(c => new { c.Id, c.FullName, c.Phone, c.Address })
            .ToListAsync();

        var ids = customers.Select(x => x.Id).ToList();
        if (!ids.Any())
        {
            return new CustomerLoyaltyReportDto
            {
                Start = start,
                End = end,
                TotalCustomers = 0,
                Items = new List<CustomerLoyaltyItemDto>()
            };
        }

        var orders = await _context.OrderHeaders
            .AsNoTracking()
            .Where(o => o.CompanyId == companyId && o.IsClosed && o.CustomerId != null)
            .Where(o => ids.Contains(o.CustomerId!.Value))
            .Where(o => o.CloseTime.HasValue && o.CloseTime.Value >= start && o.CloseTime.Value <= end)
            .Select(o => new
            {
                CustomerId = o.CustomerId!.Value,
                o.TotalAmount,
                o.CloseTime
            })
            .ToListAsync();

        var byCustomer = orders
            .GroupBy(x => x.CustomerId)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    Total = g.Sum(x => x.TotalAmount),
                    Count = g.Count(),
                    LastAt = g.Max(x => x.CloseTime),
                    LastTotal = g.OrderByDescending(x => x.CloseTime).Select(x => x.TotalAmount).FirstOrDefault()
                });

        var items = customers.Select(c =>
        {
            byCustomer.TryGetValue(c.Id, out var s);
            return new CustomerLoyaltyItemDto
            {
                CustomerId = c.Id,
                FullName = c.FullName ?? "",
                Phone = c.Phone ?? "",
                Address = c.Address,
                TotalSpent = s?.Total ?? 0,
                OrderCount = s?.Count ?? 0,
                LastOrderAt = s?.LastAt,
                LastOrderTotal = s?.LastTotal ?? 0
            };
        })
        .OrderByDescending(x => x.TotalSpent)
        .ThenByDescending(x => x.OrderCount)
        .Take(Math.Max(10, Math.Min(take, 1000)))
        .ToList();

        return new CustomerLoyaltyReportDto
        {
            Start = start,
            End = end,
            TotalCustomers = await custQuery.CountAsync(),
            Items = items
        };
    }

    public async Task<ShiftReportDto> GetShiftReportAsync(Guid shiftId, Guid companyId, bool includeOpenTables = false)
    {
        var shift = await _context.CashShifts
            .Include(s => s.OpenedByUser)
            .FirstOrDefaultAsync(s => s.Id == shiftId && s.CompanyId == companyId);

        if (shift == null) throw new Exception("Növbə tapılmadı!");

        // Növbə bitməyibsə indiki vaxtı götürürük
        var endTime = shift.EndTime ?? DateTime.UtcNow.AddHours(4);

        // Açıq masalar: yalnız bu növbənin StartTime-indən sonra açılmış aktiv sifarişlər
        var summary = await GetGeneralSummaryAsync(
            shift.StartTime,
            endTime,
            companyId,
            includeOpenTables,
            includeOpenTables ? shift.StartTime : null,
            shift.Id);

        var expensesTotal = await _context.CashShiftExpenses
            .AsNoTracking()
            .Where(e => e.CompanyId == companyId && e.CashShiftId == shift.Id && !e.IsDeleted)
            .SumAsync(e => e.Amount);

        return new ShiftReportDto
        {
            ShiftId = shift.Id,
            StartTime = shift.StartTime,
            EndTime = shift.EndTime,
            OpenedBy = shift.OpenedByUser?.FullName ?? shift.OpenedByUser?.Username ?? "Məlum deyil",
            TotalRevenue = summary.TotalRevenue,
            TotalCost = summary.TotalCost,
            TotalCash = summary.TotalCash,
            TotalCard = summary.TotalCard,
            OrderCount = summary.OrderCount,
            ClosedRevenue = summary.ClosedRevenue,
            ClosedOrderCount = summary.ClosedOrderCount,
            OpenTablesIncluded = summary.OpenTablesIncluded,
            OpenRevenueAdded = summary.OpenRevenueAdded,
            OpenCostAdded = summary.OpenCostAdded,
            OpenCashAdded = summary.OpenCashAdded,
            OpenCardAdded = summary.OpenCardAdded,
            OpenOrderCount = summary.OpenOrderCount,
            ServiceFeeRevenue = summary.ServiceFeeRevenue,
            DepositRevenue = summary.DepositRevenue,
            TotalDiscountAmount = summary.TotalDiscountAmount,
            DailyReports = summary.DailyReports,
            CustomPaymentTotals = summary.CustomPaymentTotals,
            ShiftExpensesTotal = expensesTotal,
            OpeningDepositAmount = shift.OpeningDepositAmount
        };
    }

    public async Task<(List<ShiftReportDto> Items, int TotalCount)> GetAllShiftsAsync(int page, int pageSize, Guid companyId)
    {
        var query = _context.CashShifts
            .Include(s => s.OpenedByUser)
            .Where(s => s.CompanyId == companyId)
            .OrderByDescending(s => s.StartTime);

        var totalCount = await query.CountAsync();

        var shifts = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var shiftReports = new List<ShiftReportDto>();

        foreach (var shift in shifts)
        {
            // Növbənin bitiş vaxtı (bağlıdırsa öz vaxtı, açıqdırsa indiki vaxt)
            var endTime = shift.EndTime ?? DateTime.UtcNow.AddHours(4);

            // 🔥 ƏSAS DÜZƏLİŞ: .AddHours(4) hissəsini SQL-dən yığışdırdıq 
            // Çünki datan onsuz da local vaxtladır.
            var shiftOrders = await _context.OrderHeaders
                .Include(o => o.OrderDetails)
                .ThenInclude(d => d.Product)
                .Where(o => o.IsClosed && o.CompanyId == companyId && o.CloseTime.HasValue &&
                            o.CashShiftId == shift.Id)
                .Select(o => new {
                    o.TotalAmount,
                    o.BehAmount,
                    o.ServiceAmount,
                    o.PaidCash,
                    o.PaidCard,
                    o.CustomPaymentMethodId,
                    CustomMethodName = o.CustomPaymentMethod != null ? o.CustomPaymentMethod.NameAz : null,
                    Cost = o.OrderDetails.Sum(d => (decimal)d.Quantity * (d.Product != null ? d.Product.CostPrice : 0))
                })
                .ToListAsync();

            var shiftCustomRows = shiftOrders
                .Where(x => x.CustomPaymentMethodId.HasValue)
                .Select(x => (
                    x.CustomPaymentMethodId!.Value,
                    x.CustomMethodName ?? "",
                    x.TotalAmount,
                    x.BehAmount,
                    x.PaidCash,
                    x.PaidCard))
                .ToList();
            var shiftCustomTotals = BuildCustomPaymentTotalsFromRows(shiftCustomRows);

            var shiftRevenue = shiftOrders.Sum(x => x.TotalAmount);
            var shiftCashRaw = shiftOrders.Sum(x =>
                OrderPaymentNet.NaqdKartReportGross(x.TotalAmount, x.BehAmount, x.ServiceAmount, x.PaidCash, x.PaidCard, x.CustomPaymentMethodId).Cash);
            var shiftCardRaw = shiftOrders.Sum(x =>
                OrderPaymentNet.NaqdKartReportGross(x.TotalAmount, x.BehAmount, x.ServiceAmount, x.PaidCash, x.PaidCard, x.CustomPaymentMethodId).Card);
            var shiftCustomSum = shiftCustomTotals.Sum(x => x.Amount);
            var (shiftCash, shiftCard) = OrderPaymentNet.ReconcileReportPaymentTotals(
                shiftRevenue, shiftCashRaw, shiftCardRaw, shiftCustomSum);
            var shiftService = shiftOrders.Sum(x => x.ServiceAmount);

            shiftReports.Add(new ShiftReportDto
            {
                ShiftId = shift.Id,
                StartTime = shift.StartTime,
                EndTime = shift.EndTime,
                OpeningDepositAmount = shift.OpeningDepositAmount,
                OpenedBy = shift.OpenedByUser?.Username ?? "Sistem",
                TotalRevenue = shiftRevenue,
                TotalCost = shiftOrders.Sum(x => x.Cost),
                TotalCash = shiftCash,
                TotalCard = shiftCard,
                OrderCount = shiftOrders.Count,
                ServiceFeeRevenue = shiftService,
                CustomPaymentTotals = shiftCustomTotals
            });
        }

        return (shiftReports, totalCount);
    }
}