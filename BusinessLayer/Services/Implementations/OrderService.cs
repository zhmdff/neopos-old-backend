using AutoMapper;
using BusinessLayer.Helpers;
using BusinessLayer.DTOs.Audit;
using BusinessLayer.DTOs.Kitchen;
using BusinessLayer.DTOs.OrderDetail;
using BusinessLayer.DTOs.OrderHeader;
using BusinessLayer.Services.Abstractions;
using BusinessLayer.Utilities;
using DAL.Server.Context;
using Domain.Common.Entities;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace BusinessLayer.Services.Implementations;

public class OrderService : IOrderService
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;
    private readonly IAuditLogService _auditLogService;
    private readonly IHallTimeDiscountRuleService _hallTimeDiscountRuleService;
    private readonly ITcpPrinterService _tcpPrinterService;
    private DateTime AzTime => DateTime.SpecifyKind(DateTime.UtcNow.AddHours(4), DateTimeKind.Unspecified);

    public OrderService(
        AppDbContext context,
        IMapper mapper,
        IAuditLogService auditLogService,
        IHallTimeDiscountRuleService hallTimeDiscountRuleService,
        ITcpPrinterService tcpPrinterService)
    {
        _context = context;
        _mapper = mapper;
        _auditLogService = auditLogService;
        _hallTimeDiscountRuleService = hallTimeDiscountRuleService;
        _tcpPrinterService = tcpPrinterService;
    }

    /// <summary>Telegram/audit: trailing sıfırlar atılır (43.2000 → 43.2).</summary>
    private static string FormatAznAudit(decimal amount)
    {
        var s = amount.ToString("F4", CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.');
        return s;
    }

    /// <summary>Paralel/təkrar POST /Orders/open — eyni ClientOrderId ilə ikinci INSERT (PK_OrderHeaders).</summary>
    private static bool IsDuplicateOrderHeaderKey(DbUpdateException ex)
    {
        for (Exception? cur = ex; cur != null; cur = cur.InnerException)
        {
            var m = cur.Message;
            if (m.Contains("23505", StringComparison.Ordinal) &&
                m.Contains("PK_OrderHeaders", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private async Task ApplyGuestCountToActiveOrderIfProvidedAsync(Guid tableId, Guid companyId, int? guestCount)
    {
        if (!guestCount.HasValue) return;
        if (guestCount.Value < 0) throw new Exception("Qonaq sayı mənfi ola bilməz.");

        var active = await _context.OrderHeaders
            .FirstOrDefaultAsync(o => o.TableId == tableId && !o.IsClosed && o.CompanyId == companyId);
        if (active == null) return;
        if (active.GuestCount == guestCount) return;

        active.GuestCount = guestCount.Value;
        _context.OrderHeaders.Update(active);
        await _context.SaveChangesAsync();
    }

    /// <summary>Hərəkət tarixçəsi: əlavə üsul seçilibsə nağd/kart əvəzinə etiket + məbləğ.</summary>
    private async Task<string> FormatClosedOrderAuditPaymentAsync(
        Guid companyId,
        Guid? customPaymentMethodId,
        decimal payableAmount,
        decimal netCash,
        decimal netCard)
    {
        if (!customPaymentMethodId.HasValue)
            return $"Nağd: {FormatAznAudit(netCash)}, Kart: {FormatAznAudit(netCard)}";
        var name = await _context.CompanyPaymentMethods
            .AsNoTracking()
            .Where(m => m.Id == customPaymentMethodId.Value && m.CompanyId == companyId && !m.IsDeleted)
            .Select(m => m.NameAz)
            .FirstOrDefaultAsync();
        var label = string.IsNullOrWhiteSpace(name) ? "Xüsusi ödəniş" : name.Trim();
        return $"{label}: {FormatAznAudit(payableAmount)} ₼";
    }

    public async Task<OrderHeaderGetDto> OpenOrderAsync(OrderHeaderPostDto dto)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var table = await _context.Tables
                .Include(t => t.Hall)
                .FirstOrDefaultAsync(t => t.Id == dto.TableId && t.CompanyId == dto.CompanyId);

            if (table == null) throw new Exception("Masa tapılmadı!");

            var activeOnTable = await _context.OrderHeaders
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.TableId == dto.TableId && !o.IsClosed && o.CompanyId == dto.CompanyId);

            if (dto.ClientOrderId.HasValue && dto.ClientOrderId.Value != Guid.Empty)
            {
                var prior = await _context.OrderHeaders
                    .AsNoTracking()
                    .FirstOrDefaultAsync(o => o.Id == dto.ClientOrderId.Value && o.CompanyId == dto.CompanyId);

                if (prior != null)
            {
                if (prior.TableId != dto.TableId)
                    throw new Exception("Sifariş identifikatoru bu masa ilə uyğun gəlmir.");
                if (!prior.IsClosed)
                {
                    await ApplyGuestCountToActiveOrderIfProvidedAsync(dto.TableId, dto.CompanyId, dto.GuestCount);
                    return await GetActiveOrderContentsAsync(prior.TableId, dto.CompanyId);
                }

                // Oflayn növbə: clientOrderId artıq bağlanmış köhnə çekdir.
                if (activeOnTable != null)
                {
                    await ApplyGuestCountToActiveOrderIfProvidedAsync(dto.TableId, dto.CompanyId, dto.GuestCount);
                    return await GetActiveOrderContentsAsync(dto.TableId, dto.CompanyId);
                }
                // Masa boşdur — aşağıda yeni açılış (bağlı PK-yə yazmadan).
            }
            else if (activeOnTable != null)
                {
                    await ApplyGuestCountToActiveOrderIfProvidedAsync(dto.TableId, dto.CompanyId, dto.GuestCount);
                    return await GetActiveOrderContentsAsync(dto.TableId, dto.CompanyId);
                }
            }
            else if (activeOnTable != null)
            {
                // Cursor yeniləmə etdi — clientOrderId yoxdur (məs. onlayn open növbəsi); masa artıq aktivdirsə mövcud sifarişi qaytar.
                await ApplyGuestCountToActiveOrderIfProvidedAsync(dto.TableId, dto.CompanyId, dto.GuestCount);
                return await GetActiveOrderContentsAsync(dto.TableId, dto.CompanyId);
            }

            var order = _mapper.Map<OrderHeader>(dto);
            order.Id = Guid.NewGuid();
            if (dto.ClientOrderId.HasValue && dto.ClientOrderId.Value != Guid.Empty)
            {
                var idTaken = await _context.OrderHeaders.AsNoTracking()
                    .AnyAsync(o => o.Id == dto.ClientOrderId.Value && o.CompanyId == dto.CompanyId);
                if (!idTaken)
                    order.Id = dto.ClientOrderId.Value;
            }
            else if (order.Id == Guid.Empty)
                order.Id = Guid.NewGuid();

            order.OpenTime = AzTime;
            order.CheckNumber = $"CH-{AzTime:yyyyMMddHHmm}";
            order.IsClosed = false;
            order.CompanyId = dto.CompanyId;

            order.ServicePercentage = table.Hall.ServicePercentage;
            order.DepositAmount = table.DepositAmount ?? 0;
            order.DepositStartTime = table.DepositStartTime;
            order.DepositEndTime = table.DepositEndTime;
            order.TotalAmount = order.DepositAmount;
            order.CreatedBy = dto.CreatedBy ?? "Admin";
            if (dto.GuestCount.HasValue)
            {
                if (dto.GuestCount.Value < 0)
                    throw new Exception("Qonaq sayı mənfi ola bilməz.");
                order.GuestCount = dto.GuestCount;
            }

            var timeDiscount = await _hallTimeDiscountRuleService.ResolveActiveForOpenOrderAsync(
                table.HallId, dto.CompanyId, AzTime);
            if (timeDiscount != null)
                HallTimeDiscountHelper.ApplyToOrder(order, timeDiscount);

            // CashShiftId yalnız ödəniş/çek bağlananda aktiv növbəyə yazılır (köhnə növbədə açılıb növbə B-də ödəniləndə B-yə düşsün).

            table.Status = TableStatus.Occupied;
            _context.Tables.Update(table);

            try
            {
                await _context.OrderHeaders.AddAsync(order);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsDuplicateOrderHeaderKey(ex))
            {
                await transaction.RollbackAsync();
                if (dto.ClientOrderId is { } cid && cid != Guid.Empty)
                {
                    var dup = await _context.OrderHeaders.AsNoTracking()
                        .FirstOrDefaultAsync(o => o.Id == cid && o.CompanyId == dto.CompanyId);
                    if (dup != null && !dup.IsClosed)
                    {
                        await ApplyGuestCountToActiveOrderIfProvidedAsync(dup.TableId, dto.CompanyId, dto.GuestCount);
                        return await GetActiveOrderContentsAsync(dup.TableId, dto.CompanyId);
                    }
                }
                throw;
            }

            await _auditLogService.LogActionAsync(new AuditLogPostDto
            {
                UserId = Guid.Empty, 
                UserName = dto.CreatedBy ?? "Admin",
                Action = "SİFARİŞ AÇILDI",
                TableName = table.NameAz,
                HallName = table.Hall.NameAz,
                Description = $"{table.NameAz} masasında yeni sifariş başladıldı.",
                CompanyId = dto.CompanyId
            });

            await transaction.CommitAsync();
            return await GetActiveOrderContentsAsync(table.Id, dto.CompanyId);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<OrderHeaderGetDto> AddItemsToOrderAsync(Guid orderId, List<OrderDetailPostDto> items, Guid companyId)
    {
        async Task<OrderHeader> loadOrderAsync()
        {
            return await _context.OrderHeaders
                .Include(o => o.OrderDetails)
                .Include(o => o.Table).ThenInclude(t => t.Hall)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.CompanyId == companyId)
                ?? throw new Exception("Sifariş tapılmadı!");
        }

        async Task<(List<string> addedItemsLog, bool tagAfterKitchen)> applyItemsAsync(OrderHeader order)
        {
            var hallName = order.Table?.Hall?.NameAz?.ToLower().Trim() ?? "";
            bool isTakeaway = hallName.Contains("çöl") || hallName.Contains("takeaway") || hallName.Contains("çatdırılma") || hallName.Contains("delivery");
            int delay = 0;

            List<string> addedItemsLog = [];

            var detailIds = order.OrderDetails.Select(d => d.Id).ToList();
            Dictionary<Guid, double> kitchenNetByDetail = new();
            if (detailIds.Count > 0)
            {
                kitchenNetByDetail = await _context.KitchenOperations.AsNoTracking()
                    .Where(k => k.OrderHeaderId == order.Id && k.CompanyId == companyId && detailIds.Contains(k.OrderDetailId))
                    .GroupBy(k => k.OrderDetailId)
                    .ToDictionaryAsync(g => g.Key, g => g.Sum(x => (double)x.Quantity));
            }

            var anyIncreaseOnKitchenSentLine = false;

            foreach (var item in items)
            {
                var product = await _context.Products
                    .Include(p => p.Variants)
                    .FirstOrDefaultAsync(p => p.Id == item.ProductId && p.CompanyId == companyId && !p.IsDeleted);

                if (product == null) continue;

                ProductVariant? variant = null;
                if (item.ProductVariantId.HasValue)
                {
                    variant = product.Variants?.FirstOrDefault(v =>
                        v.Id == item.ProductVariantId.Value && !v.IsDeleted && v.CompanyId == companyId);
                }

                var displayName = variant != null ? $"{product.NameAz} - {variant.NameAz}" : product.NameAz;
                addedItemsLog.Add($"{item.Quantity} x {displayName}");

                var itemNoteKey = (item.ItemNote ?? "").Trim();
                var itemCompKey = (item.KitchenCompositionNote ?? "").Trim();
                var existingDetail = order.OrderDetails
                    .FirstOrDefault(d =>
                        d.ProductId == item.ProductId &&
                        d.ProductVariantId == item.ProductVariantId &&
                        (d.ItemNote ?? "").Trim() == itemNoteKey &&
                        (d.KitchenCompositionNote ?? "").Trim() == itemCompKey);

                if (existingDetail != null)
                {
                    var net = kitchenNetByDetail.TryGetValue(existingDetail.Id, out var nv) ? nv : 0d;
                    if (existingDetail.IsSent || net > 1e-9)
                        anyIncreaseOnKitchenSentLine = true;
                    existingDetail.Quantity += item.Quantity;
                    existingDetail.TotalPrice = (decimal)existingDetail.Quantity * existingDetail.Price;
                    // IsSent saxlanılır: əvvəl mətbəxə gedibsə true qalır — yalnız delta növbəti göndərişlə gedir.
                }
                else
                {
                    decimal finalPrice;
                    if (variant != null)
                    {
                        finalPrice = variant.Price;
                        if (isTakeaway && variant.DeliveryPrice.HasValue && variant.DeliveryPrice.Value > 0)
                            finalPrice = variant.DeliveryPrice.Value;
                    }
                    else
                    {
                        finalPrice = product.SalePrice;
                        if (isTakeaway && product.DeliveryPrice.HasValue && product.DeliveryPrice.Value > 0)
                            finalPrice = product.DeliveryPrice.Value;
                    }

                    var detail = new OrderDetail
                    {
                        OrderHeaderId = orderId,
                        ProductId = item.ProductId,
                        ProductVariantId = item.ProductVariantId,
                        ProductVariantName = variant?.NameAz,
                        ProductName = displayName ?? "Məhsul",
                        Price = finalPrice,
                        Quantity = item.Quantity,
                        TotalPrice = (decimal)item.Quantity * finalPrice,
                        ItemNote = item.ItemNote ?? "",
                        KitchenCompositionNote = item.KitchenCompositionNote ?? "",
                        CompanyId = companyId,
                        CreatedBy = order.CreatedBy,
                        CreatedAt = AzTime.AddMilliseconds(delay++),
                        IsSent = false
                    };
                    await _context.OrderDetails.AddAsync(detail);
                }
            }

            return (addedItemsLog, anyIncreaseOnKitchenSentLine);
        }

        // Waiter app-də eyni order-ə paralel dəyişikliklər olanda bəzən detail silinmiş/yenilənmiş olur.
        // 1 dəfə reload+retry edərək ofisiantın "ilişib qalma" problemini aradan qaldırırıq.
        OrderHeader order = await loadOrderAsync();
        var applyResult = await applyItemsAsync(order);
        var addedItemsLog = applyResult.addedItemsLog;
        var addItemsTagAfterKitchen = applyResult.tagAfterKitchen;
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            foreach (var e in _context.ChangeTracker.Entries().ToList())
            {
                e.State = EntityState.Detached;
            }
            order = await loadOrderAsync();
            applyResult = await applyItemsAsync(order);
            addedItemsLog = applyResult.addedItemsLog;
            addItemsTagAfterKitchen = applyResult.tagAfterKitchen;
            await _context.SaveChangesAsync();
        }

        if (addedItemsLog.Count != 0)
        {
            string logDescription = string.Join(", ", addedItemsLog);
            var tgAddSuffix = $" [[NeoPos:afterKitchen:{(addItemsTagAfterKitchen ? 1 : 0)}]]";

            await _auditLogService.LogActionAsync(new AuditLogPostDto
            {
                UserName = order.CreatedBy,
                Action = "MƏHSUL ƏLAVƏSİ",
                TableName = order.Table?.NameAz ?? "---",
                HallName = order.Table?.Hall?.NameAz ?? "---",
                Description = $"{order.Table?.NameAz} masasına əlavə edildi: {logDescription}{tgAddSuffix}",
                CompanyId = companyId
            });
        }

        await RecalculateOrderTotal(order);
        return await GetActiveOrderContentsAsync(order.TableId, companyId);
    }

    public async Task<OrderHeaderGetDto> GetActiveOrderContentsAsync(Guid tableId, Guid companyId)
    {
        var order = await _context.OrderHeaders
            .Include(o => o.Table).ThenInclude(t => t.Hall)
            .Include(o => o.OrderDetails)
                .ThenInclude(d => d.Product)
                    .ThenInclude(p => p.Workshop)
            .Include(o => o.SplitPayments)
            .Include(o => o.Customer)
            .Include(o => o.CustomPaymentMethod)
            .Where(o => o.TableId == tableId && !o.IsClosed && o.CompanyId == companyId)
            .OrderByDescending(o => o.OpenTime)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (order == null)
            return _mapper.Map<OrderHeaderGetDto>(order);

        order.OrderDetails = order.OrderDetails.OrderBy(d => d.CreatedAt).ToList();

        var dto = _mapper.Map<OrderHeaderGetDto>(order);
        if (dto?.OrderDetails is { Count: > 0 })
        {
            var netByDetail = await _context.KitchenOperations
                .AsNoTracking()
                .Where(k => k.OrderHeaderId == order.Id && k.CompanyId == companyId)
                .GroupBy(k => k.OrderDetailId)
                .Select(g => new { DetailId = g.Key, Net = g.Sum(x => x.Quantity) })
                .ToDictionaryAsync(x => x.DetailId, x => x.Net);

            foreach (var d in dto.OrderDetails)
            {
                var net = netByDetail.TryGetValue(d.Id, out var v) ? v : 0d;
                d.KitchenSentQuantity = net > 0 ? net : 0d;
            }
        }

        return dto;
    }

    public async Task<OrderHeaderGetDto> UpdateOrderItemAsync(Guid detailId, OrderDetailUpdateDto dto)
    {
        // 1. Məhsulu bazadan tapırıq
        var detail = await _context.OrderDetails
            .Include(d => d.OrderHeader)
            .FirstOrDefaultAsync(d => d.Id == detailId && d.CompanyId == dto.CompanyId);

        if (detail == null) throw new Exception("Məhsul tapılmadı!");

        var hadKitchenBeforeAction =
            detail.IsSent ||
            (await _context.KitchenOperations.AsNoTracking()
                .Where(k => k.OrderDetailId == detail.Id && k.CompanyId == dto.CompanyId)
                .SumAsync(k => (double?)k.Quantity) ?? 0d) > 1e-9;
        var tgKitchenSuffix = $" [[NeoPos:afterKitchen:{(hadKitchenBeforeAction ? 1 : 0)}]]";

        var prevQty = detail.Quantity;
        var prevNote = detail.ItemNote ?? "";
        var prevPrice = detail.Price;
        var prevProductName = (detail.ProductName ?? "").Trim();
        var lineProductNameSnapshot = detail.ProductName;
        var lineTotalBeforeSnapshot = (decimal)prevQty * prevPrice;

        var newQty = dto.Quantity;
        var newNote = dto.ItemNote ?? "";
        var dtoPrice = dto.Price;

        static bool QtyDiff(double a, double b) => Math.Abs(a - b) > 1e-9;

        var qtyChanged = QtyDiff(prevQty, newQty);
        var noteChanged = prevNote != newNote;
        var priceChanged = dtoPrice.HasValue && prevPrice != dtoPrice.Value;
        var nameChanged = !string.IsNullOrEmpty(dto.ProductName)
            && !string.Equals(prevProductName, dto.ProductName.Trim(), StringComparison.OrdinalIgnoreCase);

        var isCancel = newQty == 0;

        // Heç nə dəyişməyibsə — DB yazılmır, audit/Telegram yox (məs. qiymət sahəsində ara simvollar).
        if (!isCancel && !qtyChanged && !noteChanged && !priceChanged && !nameChanged)
            return await GetActiveOrderContentsAsync(detail.OrderHeader.TableId, dto.CompanyId);

        // 2. Yalnız qeyd dəyişəndə "göndərilməyib" — miqdar artımında IsSent=true qalmalıdır,
        // əks halda silmə hard-delete və ya tam mətbəx ləğvi yanlış işləyir.
        if (prevNote != newNote)
            detail.IsSent = false;

        detail.Quantity = newQty;
        detail.ItemNote = newNote;

        if (dtoPrice.HasValue)
            detail.Price = dtoPrice.Value;

        if (!string.IsNullOrEmpty(dto.ProductName))
            detail.ProductName = dto.ProductName;

        // 5. Cəmi məbləği yenidən hesablayırıq
        detail.TotalPrice = (decimal)detail.Quantity * detail.Price;

        await _context.SaveChangesAsync();

        // 6. Sifarişi tapıb ümumi məbləği (TotalAmount) yenidən hesablatdırırıq
        var order = await _context.OrderHeaders
            .Include(o => o.OrderDetails)
            .Include(o => o.Table).ThenInclude(t => t.Hall)
            .FirstAsync(o => o.Id == detail.OrderHeaderId && o.CompanyId == dto.CompanyId);

        var reasonNow = (dto.CancelReason ?? "").Trim();
        if (reasonNow.Length > 500) reasonNow = reasonNow[..500];
        var reasonPart = string.IsNullOrWhiteSpace(reasonNow) ? "" : $" Səbəb: {reasonNow}";
        var noteNow = newNote.Trim();
        var notePart = string.IsNullOrWhiteSpace(noteNow) ? "" : $" Qeyd: {noteNow}";

        if (isCancel)
        {
            await _auditLogService.LogActionAsync(new AuditLogPostDto
            {
                UserName = detail.CreatedBy ?? order.CreatedBy ?? "—",
                Action = "MƏHSUL SİLİNDİ ❗",
                TableName = order.Table.NameAz,
                HallName = order.Table?.Hall?.NameAz,
                Description = $"{lineProductNameSnapshot} məhsulu ləğv edildi. Miqdar: {prevQty} → 0.{reasonPart}{tgKitchenSuffix}",
                CompanyId = dto.CompanyId,
                LineProductName = lineProductNameSnapshot,
                LineQuantity = (decimal)prevQty,
                LineUnitPrice = prevPrice,
                LineTotal = lineTotalBeforeSnapshot
            });
        }
        else
        {
            var segments = new List<string>();
            if (qtyChanged)
                segments.Add($"Miqdar: {prevQty} → {newQty}");
            if (priceChanged)
                segments.Add($"Qiymət: {prevPrice:0.00} → {detail.Price:0.00} ₼");
            if (nameChanged)
                segments.Add($"Ad: {prevProductName} → {(detail.ProductName ?? "").Trim()}");

            var desc = $"{(detail.ProductName ?? "").Trim()} məhsulu redaktə edildi";
            if (segments.Count > 0)
                desc += ". " + string.Join(", ", segments);
            desc += "." + notePart;
            if (qtyChanged)
                desc += tgKitchenSuffix;

            await _auditLogService.LogActionAsync(new AuditLogPostDto
            {
                UserName = detail.CreatedBy ?? order.CreatedBy ?? "—",
                Action = "MƏHSUL REDAKTƏSİ",
                TableName = order.Table.NameAz,
                HallName = order.Table?.Hall?.NameAz,
                Description = desc,
                CompanyId = dto.CompanyId,
                LineProductName = null,
                LineQuantity = null,
                LineUnitPrice = null,
                LineTotal = null
            });
        }

        await RecalculateOrderTotal(order);

        // 7. Sifarişin son halını qaytarırıq
        return await GetActiveOrderContentsAsync(order.TableId, dto.CompanyId);
    }

    public async Task<OrderHeaderGetDto> RemoveOrderItemAsync(Guid detailId, Guid companyId, string? reason = null)
    {
        var detail = await _context.OrderDetails
            .Include(d => d.OrderHeader).ThenInclude(o => o.Table)
            .Include(d => d.OrderHeader).ThenInclude(o => o.Table.Hall)
            .FirstOrDefaultAsync(d => d.Id == detailId && d.CompanyId == companyId);

        if (detail == null) throw new Exception("Məhsul tapılmadı!");

        var hadKitchenBeforeAction =
            detail.IsSent ||
            (await _context.KitchenOperations.AsNoTracking()
                .Where(k => k.OrderDetailId == detail.Id && k.CompanyId == companyId)
                .SumAsync(k => (double?)k.Quantity) ?? 0d) > 1e-9;
        var tgKitchenSuffix = $" [[NeoPos:afterKitchen:{(hadKitchenBeforeAction ? 1 : 0)}]]";

        var orderId = detail.OrderHeaderId;
        var tableId = detail.OrderHeader.TableId;
        var tableName = detail.OrderHeader.Table?.NameAz ?? "NAMƏLUM MASA";
        var hallName = detail.OrderHeader.Table?.Hall?.NameAz ?? "ZAL QEYD EDİLMƏYİB";

        var reasonTrim = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (reasonTrim != null && reasonTrim.Length > 500)
            reasonTrim = reasonTrim[..500];

        var lineProductName = detail.ProductName;
        var lineQty = (decimal)detail.Quantity;
        var lineUnitPrice = detail.Price;
        var lineTotal = detail.TotalPrice;

        if (!detail.IsSent)
        {
            _context.OrderDetails.Remove(detail);
        }
        else
        {
            detail.Quantity = 0;
            detail.TotalPrice = 0;
            detail.IsSent = false;
            _context.OrderDetails.Update(detail);
        }

        await _context.SaveChangesAsync();

        var order = await _context.OrderHeaders
            .Include(o => o.OrderDetails)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.CompanyId == companyId);

        var desc = reasonTrim == null
            ? $"{lineProductName} məhsulu siyahıdan silindi/ləğv edildi.{tgKitchenSuffix}"
            : $"{lineProductName} məhsulu siyahıdan silindi/ləğv edildi. Səbəb: {reasonTrim}{tgKitchenSuffix}";

        await _auditLogService.LogActionAsync(new AuditLogPostDto
        {
            UserName = detail.CreatedBy,
            Action = "MƏHSUL SİLİNDİ ❗",
            TableName = tableName, 
            HallName = hallName,
            Description = desc,
            CompanyId = companyId,
            LineProductName = lineProductName,
            LineQuantity = lineQty,
            LineUnitPrice = lineUnitPrice,
            LineTotal = lineTotal
        }) ;

        if (order != null)
        {
            await RecalculateOrderTotal(order);
            return await GetActiveOrderContentsAsync(tableId, companyId);
        }
        return null;
    }

    public async Task<bool> DeleteOrderAsync(Guid orderId, Guid companyId)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var order = await _context.OrderHeaders
                .Include(o => o.OrderDetails)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.CompanyId == companyId);

            if (order == null) return true;

            if (order.OrderDetails.Any(d => d.Quantity > 0))
                throw new Exception("İçində məhsul olan sifarişi silmək olmaz!");

            var table = await _context.Tables.FirstOrDefaultAsync(t => t.Id == order.TableId && t.CompanyId == companyId);
            if (table != null)
            {
                table.Status = TableStatus.Empty;
                _context.Entry(table).State = EntityState.Modified;
            }

            _context.OrderHeaders.Remove(order);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
            return true;
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<OrderHeaderGetDto> UpdateServiceFeeAsync(Guid orderId, decimal newPercentage, Guid companyId)
    {
        if (newPercentage < 0) throw new Exception("Xidmət haqqı mənfi ola bilməz!");

        var order = await _context.OrderHeaders
            .Include(o => o.OrderDetails)
            .Include(o => o.Table).ThenInclude(t => t.Hall)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.CompanyId == companyId);

        if (order == null) throw new Exception("Sifariş tapılmadı!");

        order.ServicePercentage = newPercentage;
        await RecalculateOrderTotal(order);

        await _auditLogService.LogActionAsync(new AuditLogPostDto
        {
            UserName = order.CreatedBy,
            Action = "SERVİS HAQQI DƏYİŞİKLİYİ ⚙️",
            TableName = order.Table.NameAz,
            Description = $"{order.Table.NameAz} masasının xidmət haqqı %{newPercentage} olaraq yeniləndi.",
            CompanyId = companyId
        });

        return await GetActiveOrderContentsAsync(order.TableId, companyId);
    }

    // Cursor yeniləmə etdi
    public async Task<OrderHeaderGetDto?> UpdateOrderDepositAsync(Guid orderId, decimal amount, TimeSpan? start, TimeSpan? end, Guid companyId)
    {
        var order = await _context.OrderHeaders
            .Include(o => o.OrderDetails)
            .Include(o => o.Table).ThenInclude(t => t.Hall)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.CompanyId == companyId);

        if (order == null) return null; // Cursor yeniləmə etdi (əvvəl exception atırdı)

        order.DepositAmount = amount;
        order.DepositStartTime = start;
        order.DepositEndTime = end;

        await RecalculateOrderTotal(order);
        await _auditLogService.LogActionAsync(new AuditLogPostDto
        {
            UserName = order.CreatedBy,
            Action = "DEPOZİT DƏYİŞİKLİYİ 💰",
            TableName = order.Table.NameAz,
            HallName = order.Table.Hall.NameAz,
            Description = $"{order.Table.NameAz} masasına {amount} ₼ depozit təyin edildi",
            CompanyId = companyId
        });

        return await GetActiveOrderContentsAsync(order.TableId, companyId);
    }

    public async Task<OrderHeaderGetDto> UpdateOrderDiscountAsync(Guid orderId, decimal value, bool isPercentage, Guid companyId)
    {
        var order = await _context.OrderHeaders
            .Include(o => o.OrderDetails)
            .Include(o => o.Table).ThenInclude(t => t.Hall)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.CompanyId == companyId);

        if (order == null) throw new Exception("Sifariş tapılmadı!");

        order.IsPercentageDiscount = isPercentage;
        if (isPercentage)
        {
            order.DiscountPercentage = value;
            order.DiscountAmount = 0;
        }
        else
        {
            order.DiscountAmount = value;
            order.DiscountPercentage = 0;
        }

        await RecalculateOrderTotal(order);

        string discountText = isPercentage ? $"%{value}" : $"{value} ₼";
        await _auditLogService.LogActionAsync(new AuditLogPostDto
        {
            UserName = order.CreatedBy,
            Action = "ENDİRİM TƏTBİQİ 📉",
            TableName = order.Table.NameAz,
            Description = $"{order.Table.NameAz} masasına {discountText} endirim edildi.",
            CompanyId = companyId
        });
        return await GetActiveOrderContentsAsync(order.TableId, companyId);
    }

    public async Task<OrderHeaderGetDto> UpdateOrderNoteAsync(Guid orderId, string? note, Guid companyId)
    {
        var order = await _context.OrderHeaders
            .Include(o => o.OrderDetails)
            .Include(o => o.Table).ThenInclude(t => t.Hall)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.CompanyId == companyId);

        if (order == null) throw new Exception("Sifariş tapılmadı!");

        order.Note = note;
        _context.OrderHeaders.Update(order);
        await _context.SaveChangesAsync();

        return await GetActiveOrderContentsAsync(order.TableId, companyId);
    }

    public async Task<OrderHeaderGetDto> UpdateOrderGuestCountAsync(Guid orderId, int? guestCount, Guid companyId)
    {
        if (guestCount.HasValue && guestCount.Value < 0)
            throw new Exception("Qonaq sayı mənfi ola bilməz.");

        var order = await _context.OrderHeaders
            .Include(o => o.Table).ThenInclude(t => t.Hall)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.CompanyId == companyId);

        if (order == null) throw new Exception("Sifariş tapılmadı!");
        if (order.IsClosed) throw new Exception("Bağlanmış çekdə qonaq sayı dəyişmək olmaz.");

        var prevGuest = order.GuestCount;
        if (prevGuest == guestCount)
            return await GetActiveOrderContentsAsync(order.TableId, companyId);

        order.GuestCount = guestCount;
        _context.OrderHeaders.Update(order);
        await _context.SaveChangesAsync();

        await _auditLogService.LogActionAsync(new AuditLogPostDto
        {
            UserName = order.CreatedBy ?? "—",
            Action = "Qonaq sayı dəyişdi",
            TableName = order.Table?.NameAz ?? "---",
            HallName = order.Table?.Hall?.NameAz ?? "---",
            Description =
                $"Qonaq sayı: {(prevGuest.HasValue ? prevGuest.Value.ToString() : "—")} → {(guestCount.HasValue ? guestCount.Value.ToString() : "—")}.",
            CompanyId = companyId
        });

        return await GetActiveOrderContentsAsync(order.TableId, companyId);
    }

    public async Task<OrderHeaderGetDto> UpdateTableHourBonusAsync(Guid orderId, int bonusMinutes, Guid companyId)
    {
        var requested = Math.Max(0, bonusMinutes);

        var order = await _context.OrderHeaders
            .Include(o => o.Table).ThenInclude(t => t.Hall)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.CompanyId == companyId);

        if (order == null) throw new Exception("Sifariş tapılmadı!");
        if (order.IsClosed) throw new Exception("Bağlanmış çekdə saat limiti dəyişmək olmaz.");

        var merged = Math.Max(order.TableHourBonusMinutes, requested);
        if (merged == order.TableHourBonusMinutes)
            return await GetActiveOrderContentsAsync(order.TableId, companyId);

        order.TableHourBonusMinutes = merged;
        _context.OrderHeaders.Update(order);
        await _context.SaveChangesAsync();

        return await GetActiveOrderContentsAsync(order.TableId, companyId);
    }

    public async Task<OrderHeaderGetDto> ChangeOrderWaiterAsync(Guid orderId, string fullName, Guid companyId)
    {
        var trimmed = (fullName ?? "").Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new Exception("Ofisiant adı boş ola bilməz.");

        var order = await _context.OrderHeaders
            .Include(o => o.Table).ThenInclude(t => t.Hall)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.CompanyId == companyId);

        if (order == null) throw new Exception("Sifariş tapılmadı!");
        if (order.IsClosed) throw new Exception("Bağlanmış çekdə ofisiant dəyişmək olmaz.");

        var previous = order.CreatedBy ?? "";
        order.CreatedBy = trimmed;
        _context.OrderHeaders.Update(order);
        await _context.SaveChangesAsync();

        await _auditLogService.LogActionAsync(new AuditLogPostDto
        {
            UserName = trimmed,
            Action = "OFİSİANT DƏYİŞDİ",
            TableName = order.Table?.NameAz ?? "---",
            HallName = order.Table?.Hall?.NameAz ?? "---",
            Description = $"Çek ofisiantı dəyişdirildi: «{previous}» → «{trimmed}».",
            CompanyId = companyId
        });

        return await GetActiveOrderContentsAsync(order.TableId, companyId);
    }

    public async Task<bool> MarkOrderItemsAsSentAsync(MarkAsSentDto dto, Guid companyId)
    {
        if (dto.OrderDetailIds == null || !dto.OrderDetailIds.Any()) return false;

        var items = await _context.OrderDetails
            .Where(x => dto.OrderDetailIds.Contains(x.Id) && x.CompanyId == companyId)
            .ToListAsync();

        if (!items.Any()) return false;

        foreach (var item in items)
        {
            item.IsSent = true;
        }

        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<bool> CloseOrderAsync(OrderCloseDto dto, Guid companyId)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var order = await _context.OrderHeaders
                .Include(o => o.OrderDetails)
                .Include(o => o.Table)
                .ThenInclude(t => t.Hall)
                .FirstOrDefaultAsync(o => o.Id == dto.OrderId && !o.IsClosed && o.CompanyId == companyId);

            if (order == null) throw new Exception("Sifariş tapılmadı!");

            var activeShiftAtPay = await _context.CashShifts
                .FirstOrDefaultAsync(s => s.CompanyId == companyId && !s.IsClosed);
            if (activeShiftAtPay == null)
                throw new Exception("Açıq kassa növbəsi yoxdur. Əvvəl növbə açın, sonra ödənişi tamamlayın.");

            await ApplyStockDeductionForOrderAsync(order, companyId);

            var (netCash, netCard) = OrderPaymentNet.NormalizePaid(
                order.TotalAmount,
                order.BehAmount,
                dto.CashAmount,
                dto.CardAmount);
            order.PaidCash = netCash;
            order.PaidCard = netCard;
            if (dto.CustomPaymentMethodId.HasValue)
            {
                var ok = await _context.CompanyPaymentMethods.AnyAsync(m =>
                    m.Id == dto.CustomPaymentMethodId.Value &&
                    m.CompanyId == companyId &&
                    !m.IsDeleted);
                if (!ok) throw new Exception("Ödəniş üsulu tapılmadı və ya silinib.");
                order.CustomPaymentMethodId = dto.CustomPaymentMethodId;
                order.PaymentMethod = PaymentType.Custom;
            }
            else
            {
                order.CustomPaymentMethodId = null;
                order.PaymentMethod = ResolvePaymentMethod(netCash, netCard);
            }
            order.CashShiftId = activeShiftAtPay.Id;
            order.IsClosed = true;
            order.CloseTime = AzTime;
            order.CashierName = dto.CashierName ?? "Admin";

            var table = await _context.Tables.FirstOrDefaultAsync(t => t.Id == order.TableId && t.CompanyId == companyId);
            if (table != null) table.Status = TableStatus.Empty;

            await _context.SaveChangesAsync();

            var payableClose = OrderPayableAmount(order);
            var payLine = await FormatClosedOrderAuditPaymentAsync(companyId, dto.CustomPaymentMethodId, payableClose, netCash, netCard);
            await _auditLogService.LogActionAsync(new AuditLogPostDto
            {
                UserName = dto.CashierName!,
                Action = "SİFARİŞ BAĞLANDI ✅",
                TableName = order.Table?.NameAz ?? "---",
                HallName = order.Table?.Hall?.NameAz,
                Description = $"Sifariş bağlandı. Yekun: {FormatAznAudit(payableClose)} ₼. ({payLine})",
                CompanyId = companyId
            });

            await transaction.CommitAsync();

            // --- DIRECT BACKEND RECEIPT PRINTING (LAN) ---
            try
            {
                var company = await _context.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == companyId);
                if (company != null
                    && BusinessLayer.Printing.PrinterTargetParser.TryParseNetworkTarget(
                        company.CashierPrinterTarget, out var ip, out var port, out _))
                {
                    var printOrder = await _context.OrderHeaders
                        .Include(o => o.Table)
                            .ThenInclude(t => t.Hall)
                        .Include(o => o.OrderDetails)
                        .Include(o => o.Customer)
                        .Include(o => o.CustomPaymentMethod)
                        .FirstOrDefaultAsync(o => o.Id == dto.OrderId);

                    if (printOrder != null)
                    {
                        var bytes = _tcpPrinterService.GenerateKassaReceiptEscPos(
                            company, printOrder, printOrder.OrderDetails.ToList());
                        await _tcpPrinterService.SendToPrinterAsync(ip, port, bytes);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Direct Kassa Print Error: {ex.Message}");
            }

            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw new Exception("Sifariş bağlanarkən xəta: " + ex.Message);
        }
    }

    private static string FormatReopenArchiveReason(string? presetKey, string? note)
    {
        static string? PresetLabel(string? key) => key?.Trim().ToLowerInvariant() switch
        {
            "wrong_close" => "Səhvən bağladım",
            "wrong_product" => "Səhv məhsul vurmuşam",
            "customer" => "Müştəri əlaqədar",
            _ => null
        };

        var parts = new List<string>();
        var pl = PresetLabel(presetKey);
        if (!string.IsNullOrWhiteSpace(pl)) parts.Add(pl!);

        var n = (note ?? string.Empty).Trim();
        if (n.Length > 500) n = n.Substring(0, 500);
        if (n.Length > 0) parts.Add($"Əlavə: {n}");

        return parts.Count == 0 ? string.Empty : string.Join(" · ", parts);
    }

    public async Task<OrderHeaderGetDto> ReopenShiftArchiveOrderAsync(
        Guid orderId,
        Guid companyId,
        Guid userId,
        string? presetKey,
        string? note)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Id == userId && u.CompanyId == companyId);

            if (user == null) throw new Exception("İstifadəçi tapılmadı.");

            var isAdmin = user.Role?.IsAdmin == true;
            var perms = user.Role?.Permissions ?? Array.Empty<int>();
            if (!isAdmin && !perms.Contains((int)Permission.ViewArchive))
                throw new Exception("Arxiv çekini yeniləmək üçün «Arxivi görə bilər» icazəsi lazımdır.");

            var activeShift = await _context.CashShifts
                .FirstOrDefaultAsync(s => s.CompanyId == companyId && !s.IsClosed);

            if (activeShift == null) throw new Exception("Açıq növbə yoxdur.");

            var order = await _context.OrderHeaders
                .Include(o => o.OrderDetails)
                .Include(o => o.SplitPayments)
                .Include(o => o.Table)
                .ThenInclude(t => t.Hall)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.CompanyId == companyId);

            if (order == null) throw new Exception("Çek tapılmadı.");
            if (!order.IsClosed) throw new Exception("Bu çek artıq aktivdir.");
            if (!order.CloseTime.HasValue || order.CloseTime.Value < activeShift.StartTime)
                throw new Exception("Bu çek cari növbənin tarixçəsinə daxil deyil.");

            var tableNameAz = order.Table?.NameAz ?? order.TableId.ToString();
            var otherActive = await _context.OrderHeaders.AnyAsync(o =>
                o.TableId == order.TableId &&
                o.CompanyId == companyId &&
                !o.IsClosed &&
                o.Id != order.Id);

            if (otherActive)
                throw new Exception($"Bu masa doludur: {tableNameAz}. Çeki tarixçədən yeniləmək mümkün deyil.");

            await ApplyStockReturnForReopenedOrderAsync(order, companyId);

            var splits = order.SplitPayments?.ToList() ?? new List<OrderSplitPayment>();
            if (splits.Count > 0)
            {
                _context.OrderSplitPayments.RemoveRange(splits);
            }

            order.PaidCash = 0;
            order.PaidCard = 0;
            order.PaymentMethod = PaymentType.Cash;
            order.CustomPaymentMethodId = null;
            order.IsClosed = false;
            order.CloseTime = null;
            order.CashShiftId = activeShift.Id;

            var table = order.Table ?? await _context.Tables.FirstAsync(t => t.Id == order.TableId && t.CompanyId == companyId);
            table.Status = TableStatus.Occupied;

            _context.OrderHeaders.Update(order);
            _context.Tables.Update(table);
            await _context.SaveChangesAsync();

            var actorName = string.IsNullOrWhiteSpace(user.FullName) ? user.Username : user.FullName!.Trim();
            var reasonLine = FormatReopenArchiveReason(presetKey, note);
            var baseDesc =
                $"{actorName} «{tableNameAz}» masasının bağlı çekini ({order.CheckNumber}) növbə tarixçəsindən yeniləyib — çek yenidən aktivdir.";
            var fullDesc = string.IsNullOrEmpty(reasonLine) ? baseDesc : $"{baseDesc} Səbəb: {reasonLine}.";
            await _auditLogService.LogActionAsync(new AuditLogPostDto
            {
                UserId = userId,
                UserName = actorName,
                Action = "Arxiv çeki yeniləndi",
                TableName = tableNameAz,
                HallName = order.Table?.Hall?.NameAz,
                Description = fullDesc,
                CompanyId = companyId,
                CreatedBy = actorName
            });

            await transaction.CommitAsync();
            return await GetActiveOrderContentsAsync(order.TableId, companyId);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<object> GetClosedOrdersAsync(
        Guid companyId,
        DateTime? date = null,
        Guid? cashShiftId = null,
        int page = 1,
        int pageSize = 10,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        var query = _context.OrderHeaders
            .Include(o => o.Table).ThenInclude(t => t.Hall)
            .Include(o => o.OrderDetails)
            .Where(o => o.CompanyId == companyId);

        if (cashShiftId.HasValue && cashShiftId.Value != Guid.Empty)
        {
            query = query.Where(o => o.CashShiftId == cashShiftId);
        }
        else if (startDate.HasValue && endDate.HasValue)
        {
            var s = startDate.Value.Date;
            var e = endDate.Value.Date.AddDays(1).AddTicks(-1);

            query = query.Where(o =>
                (o.IsClosed && o.CloseTime.HasValue && o.CloseTime.Value >= s && o.CloseTime.Value <= e) ||
                (!o.IsClosed && o.OpenTime >= s && o.OpenTime <= e));
        }
        else if (date.HasValue)
        {
            var targetDate = date.Value.Date;
            query = query.Where(o => o.OpenTime.Date == targetDate);
        }

        var statsData = await query.Select(o => new
        {
            o.TotalAmount,
            o.BehAmount,
            o.PaidCash,
            o.PaidCard,
            o.CustomPaymentMethodId,
            o.IsClosed
        }).ToListAsync();

        var stats = new
        {
            totalCash = statsData
                .Where(x => x.IsClosed)
                .Sum(x => OrderPaymentNet.NaqdKartReportExcludingCustom(x.TotalAmount, x.BehAmount, x.PaidCash, x.PaidCard, x.CustomPaymentMethodId).Cash),
            totalCard = statsData
                .Where(x => x.IsClosed)
                .Sum(x => OrderPaymentNet.NaqdKartReportExcludingCustom(x.TotalAmount, x.BehAmount, x.PaidCash, x.PaidCard, x.CustomPaymentMethodId).Card),
            totalAll = statsData.Where(x => x.IsClosed).Sum(x => x.TotalAmount),
            totalActive = statsData.Where(x => !x.IsClosed).Sum(x => x.TotalAmount),
            allCount = statsData.Count
        };

        var orders = await query
            .Include(o => o.CustomPaymentMethod)
            .OrderByDescending(o => o.OpenTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync();

        return new
        {
            Orders = _mapper.Map<List<OrderHeaderGetDto>>(orders),
            Stats = stats,
            TotalPages = (int)Math.Ceiling((double)stats.allCount / pageSize),
            CurrentPage = page
        };
    }

    private async Task RecalculateOrderTotal(OrderHeader order)
    {
        decimal foodTotal = order.OrderDetails.Sum(d => d.TotalPrice);
        decimal baseAmount = Math.Max(foodTotal, order.DepositAmount);

        decimal discountAmount = 0;
        if (order.IsPercentageDiscount)
            discountAmount = (baseAmount * order.DiscountPercentage) / 100;
        else
            discountAmount = order.DiscountAmount;

        decimal amountAfterDiscount = Math.Max(0, baseAmount - discountAmount);
        order.ServiceAmount = (amountAfterDiscount == 0) ? 0 : (baseAmount * order.ServicePercentage) / 100;
        order.TotalAmount = amountAfterDiscount + order.ServiceAmount;
        if (order.BehAmount < 0) order.BehAmount = 0;
        if (order.BehAmount > order.TotalAmount) order.BehAmount = order.TotalAmount;

        _context.OrderHeaders.Update(order);
        await _context.SaveChangesAsync();
    }

    private static decimal EffectiveBehAmount(OrderHeader o)
    {
        if (o.BehAmount <= 0) return 0m;
        return o.BehAmount > o.TotalAmount ? o.TotalAmount : o.BehAmount;
    }

    private static decimal OrderPayableAmount(OrderHeader o) =>
        Math.Max(0m, o.TotalAmount - EffectiveBehAmount(o));

    public async Task<OrderHeaderGetDto> UpdateOrderBehAsync(Guid orderId, decimal amount, Guid companyId)
    {
        var order = await _context.OrderHeaders
            .FirstOrDefaultAsync(o => o.Id == orderId && !o.IsClosed && o.CompanyId == companyId);

        if (order == null) throw new Exception("Sifariş tapılmadı!");
        if (amount < 0) throw new Exception("Beh mənfi ola bilməz.");

        order.BehAmount = amount > order.TotalAmount ? order.TotalAmount : amount;
        _context.OrderHeaders.Update(order);
        await _context.SaveChangesAsync();
        return await GetActiveOrderContentsAsync(order.TableId, companyId);
    }

    private async Task ApplyStockDeductionForOrderAsync(OrderHeader order, Guid companyId)
    {
        var saleWarehouse = await _context.Warehouses
            .FirstOrDefaultAsync(w => w.CompanyId == companyId && w.IsDefaultSale && !w.IsDeleted);

        if (saleWarehouse == null) return;

        foreach (var detail in order.OrderDetails.Where(d => d.Quantity > 0))
        {
            var product = await _context.Products.FindAsync(detail.ProductId);
            if (product == null) continue;

            decimal oldStock = product.Stock;
            product.Stock -= (decimal)detail.Quantity;

            await _context.ProductStockHistories.AddAsync(new ProductStockHistory
            {
                CompanyId = companyId,
                ProductId = detail.ProductId,
                WarehouseId = saleWarehouse.Id,
                QuantityBefore = oldStock,
                ChangeAmount = -(decimal)detail.Quantity,
                QuantityAfter = product.Stock,
                MovementType = StockMovementType.Sale,
                Note = $"Satış (Çek: {order.CheckNumber})",
                CreatedAt = AzTime,
                CreatedBy = "System"
            });
        }
    }

    private async Task ApplyStockReturnForReopenedOrderAsync(OrderHeader order, Guid companyId)
    {
        var saleWarehouse = await _context.Warehouses
            .FirstOrDefaultAsync(w => w.CompanyId == companyId && w.IsDefaultSale && !w.IsDeleted);

        if (saleWarehouse == null) return;

        foreach (var detail in order.OrderDetails.Where(d => d.Quantity > 0))
        {
            var product = await _context.Products.FindAsync(detail.ProductId);
            if (product == null) continue;

            decimal oldStock = product.Stock;
            product.Stock += (decimal)detail.Quantity;

            await _context.ProductStockHistories.AddAsync(new ProductStockHistory
            {
                CompanyId = companyId,
                ProductId = detail.ProductId,
                WarehouseId = saleWarehouse.Id,
                QuantityBefore = oldStock,
                ChangeAmount = (decimal)detail.Quantity,
                QuantityAfter = product.Stock,
                MovementType = StockMovementType.Return,
                Note = $"Arxiv çeki yeniləndi (Çek: {order.CheckNumber})",
                CreatedAt = AzTime,
                CreatedBy = "System"
            });
        }
    }

    private static Dictionary<int, decimal> ComputeSplitExpectedPayables(OrderHeader order)
    {
        var details = order.OrderDetails.Where(d => d.Quantity > 0).ToList();
        decimal totalFood = details.Sum(d => d.TotalPrice);
        decimal totalPay = OrderPayableAmount(order);

        int GroupOf(OrderDetail d) => d.SplitGroup <= 0 ? 1 : d.SplitGroup;
        var groups = details.Select(GroupOf).Distinct().OrderBy(x => x).ToList();
        var result = new Dictionary<int, decimal>();
        if (groups.Count == 0) return result;

        foreach (var g in groups)
        {
            decimal groupFood = details.Where(d => GroupOf(d) == g).Sum(d => d.TotalPrice);
            decimal ratio = totalFood > 0 ? groupFood / totalFood : 1m / groups.Count;
            result[g] = Math.Round(totalPay * ratio, 2, MidpointRounding.AwayFromZero);
        }

        decimal sum = result.Values.Sum();
        if (groups.Count > 0 && Math.Abs(sum - totalPay) > 0.01m)
        {
            var last = groups.Last();
            result[last] = result[last] + (totalPay - sum);
        }

        return result;
    }

    private static PaymentType ResolvePaymentMethod(decimal cash, decimal card)
    {
        if (cash > 0 && card > 0) return PaymentType.CashandCard;
        if (cash > 0) return PaymentType.Cash;
        return PaymentType.Card;
    }

    public async Task<OrderHeaderGetDto> UpdateOrderSplitAssignmentsAsync(Guid orderId, UpdateOrderSplitsDto dto, Guid companyId)
    {
        var order = await _context.OrderHeaders
            .Include(o => o.OrderDetails)
            .FirstOrDefaultAsync(o => o.Id == orderId && !o.IsClosed && o.CompanyId == companyId);

        if (order == null) throw new Exception("Sifariş tapılmadı!");

        if (dto.Lines != null && dto.Lines.Count > 0)
        {
            var seen = new HashSet<Guid>();
            foreach (var ld in dto.Lines)
            {
                if (!seen.Add(ld.OrderDetailId))
                    throw new Exception("Təkrarlanan sifariş sətri göndərilib.");
            }

            int delayMs = 0;
            foreach (var lineDto in dto.Lines)
            {
                var detail = order.OrderDetails.FirstOrDefault(d => d.Id == lineDto.OrderDetailId);
                if (detail == null)
                    throw new Exception("Sifariş sətri tapılmadı.");

                var raw = lineDto.Parts ?? new List<SplitPartDto>();
                var merged = raw
                    .Where(p => p.Quantity > 0.0001)
                    .GroupBy(p => p.SplitGroup < 1 ? 1 : p.SplitGroup)
                    .Select(g => new SplitPartDto { SplitGroup = g.Key, Quantity = g.Sum(x => x.Quantity) })
                    .OrderBy(p => p.SplitGroup)
                    .ToList();

                if (!merged.Any())
                {
                    detail.SplitGroup = 0;
                    continue;
                }

                double sumQ = merged.Sum(p => p.Quantity);
                if (Math.Abs(sumQ - detail.Quantity) > 0.02)
                    throw new Exception($"«{detail.ProductName}»: parça miqdarlarının cəmi ({sumQ:0.##}) sifariş miqdarına ({detail.Quantity:0.##}) bərabər olmalıdır.");

                if (merged.Count == 1)
                {
                    var p = merged[0];
                    detail.Quantity = p.Quantity;
                    detail.TotalPrice = detail.Price * (decimal)detail.Quantity;
                    detail.SplitGroup = p.SplitGroup;
                    detail.IsSent = false;
                    _context.OrderDetails.Update(detail);
                    continue;
                }

                var first = merged[0];
                detail.Quantity = first.Quantity;
                detail.TotalPrice = detail.Price * (decimal)detail.Quantity;
                detail.SplitGroup = first.SplitGroup;
                detail.IsSent = false;
                _context.OrderDetails.Update(detail);

                for (var i = 1; i < merged.Count; i++)
                {
                    var p = merged[i];
                    var nd = new OrderDetail
                    {
                        Id = Guid.NewGuid(),
                        OrderHeaderId = order.Id,
                        ProductId = detail.ProductId,
                        ProductName = detail.ProductName,
                        Price = detail.Price,
                        Quantity = p.Quantity,
                        TotalPrice = detail.Price * (decimal)p.Quantity,
                        ItemNote = detail.ItemNote,
                        CompanyId = companyId,
                        CreatedBy = detail.CreatedBy,
                        CreatedAt = AzTime.AddMilliseconds(delayMs++),
                        IsSent = false,
                        SplitGroup = p.SplitGroup
                    };
                    await _context.OrderDetails.AddAsync(nd);
                }
            }

            await _context.SaveChangesAsync();
            var reloaded = await _context.OrderHeaders
                .Include(o => o.OrderDetails)
                .FirstAsync(o => o.Id == orderId);
            await RecalculateOrderTotal(reloaded);
            return await GetActiveOrderContentsAsync(reloaded.TableId, companyId);
        }

        if (dto.Assignments == null || !dto.Assignments.Any())
        {
            foreach (var d in order.OrderDetails)
                d.SplitGroup = 0;
            await _context.SaveChangesAsync();
            var o2 = await _context.OrderHeaders.Include(x => x.OrderDetails).FirstAsync(x => x.Id == orderId);
            await RecalculateOrderTotal(o2);
            return await GetActiveOrderContentsAsync(order.TableId, companyId);
        }

        var map = dto.Assignments.ToDictionary(a => a.OrderDetailId, a => a.SplitGroup);
        foreach (var d in order.OrderDetails)
        {
            if (!map.TryGetValue(d.Id, out var g)) continue;
            d.SplitGroup = g < 1 ? 1 : g;
        }

        await _context.SaveChangesAsync();
        var o3 = await _context.OrderHeaders.Include(x => x.OrderDetails).FirstAsync(x => x.Id == orderId);
        await RecalculateOrderTotal(o3);
        return await GetActiveOrderContentsAsync(order.TableId, companyId);
    }

    public async Task<OrderHeaderGetDto?> PayOrderSplitAsync(PaySplitDto dto, Guid companyId)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var order = await _context.OrderHeaders
                .Include(o => o.OrderDetails)
                .Include(o => o.SplitPayments)
                .Include(o => o.Table)
                .ThenInclude(t => t.Hall)
                .FirstOrDefaultAsync(o => o.Id == dto.OrderId && !o.IsClosed && o.CompanyId == companyId);

            if (order == null) throw new Exception("Sifariş tapılmadı!");

            var activeShiftAtPay = await _context.CashShifts
                .FirstOrDefaultAsync(s => s.CompanyId == companyId && !s.IsClosed);
            if (activeShiftAtPay == null)
                throw new Exception("Açıq kassa növbəsi yoxdur. Əvvəl növbə açın, sonra ödənişi tamamlayın.");

            decimal cash = dto.CashAmount;
            decimal card = dto.CardAmount;
            if (cash < 0 || card < 0) throw new Exception("Məbləğ mənfi ola bilməz!");
            if (cash + card <= 0) throw new Exception("Ödəniş məbləği sıfırdan böyük olmalıdır!");

            var expected = ComputeSplitExpectedPayables(order);
            int g = dto.SplitGroup <= 0 ? 1 : dto.SplitGroup;
            if (!expected.TryGetValue(g, out var expectedForSplit))
                throw new Exception("Bu parça üçün məbləğ tapılmadı — məhsulları parçalara təyin edin.");

            decimal already = order.SplitPayments
                .Where(p => p.SplitGroup == g)
                .Sum(p => p.PaidCash + p.PaidCard);

            decimal newTotalForSplit = already + cash + card;
            if (newTotalForSplit > expectedForSplit + 0.05m)
                throw new Exception($"Bu parça üçün maksimum {expectedForSplit:F2} ₼ gözlənilir (artıq ödənilib: {already:F2}).");

            await _context.OrderSplitPayments.AddAsync(new OrderSplitPayment
            {
                OrderHeaderId = order.Id,
                SplitGroup = g,
                PaidCash = cash,
                PaidCard = card,
                CompanyId = companyId,
                CreatedBy = dto.CashierName ?? "Terminal"
            });

            await _context.SaveChangesAsync();

            decimal totalPaidAll = await _context.OrderSplitPayments
                .Where(p => p.OrderHeaderId == order.Id)
                .SumAsync(p => p.PaidCash + p.PaidCard);

            var payable = OrderPayableAmount(order);
            if (totalPaidAll + 0.02m >= payable)
            {
                await ApplyStockDeductionForOrderAsync(order, companyId);

                decimal sumCash = await _context.OrderSplitPayments
                    .Where(p => p.OrderHeaderId == order.Id)
                    .SumAsync(p => p.PaidCash);
                decimal sumCard = await _context.OrderSplitPayments
                    .Where(p => p.OrderHeaderId == order.Id)
                    .SumAsync(p => p.PaidCard);

                var (netCash, netCard) = OrderPaymentNet.NormalizePaid(
                    order.TotalAmount,
                    order.BehAmount,
                    sumCash,
                    sumCard);
                order.PaidCash = netCash;
                order.PaidCard = netCard;
                if (dto.CustomPaymentMethodId.HasValue)
                {
                    var ok = await _context.CompanyPaymentMethods.AnyAsync(m =>
                        m.Id == dto.CustomPaymentMethodId.Value &&
                        m.CompanyId == companyId &&
                        !m.IsDeleted);
                    if (!ok) throw new Exception("Ödəniş üsulu tapılmadı və ya silinib.");
                    order.CustomPaymentMethodId = dto.CustomPaymentMethodId;
                    order.PaymentMethod = PaymentType.Custom;
                }
                else
                {
                    order.CustomPaymentMethodId = null;
                    order.PaymentMethod = ResolvePaymentMethod(netCash, netCard);
                }
                order.CashShiftId = activeShiftAtPay.Id;
                order.IsClosed = true;
                order.CloseTime = AzTime;
                order.CashierName = dto.CashierName ?? "Admin";

                var table = await _context.Tables.FirstOrDefaultAsync(t => t.Id == order.TableId && t.CompanyId == companyId);
                if (table != null) table.Status = TableStatus.Empty;

                await _context.SaveChangesAsync();

                var payLineSplit = await FormatClosedOrderAuditPaymentAsync(companyId, dto.CustomPaymentMethodId, payable, netCash, netCard);
                await _auditLogService.LogActionAsync(new AuditLogPostDto
                {
                    UserName = dto.CashierName ?? "Admin",
                    Action = "SİFARİŞ BAĞLANDI (PARÇALI) ✅",
                    TableName = order.Table?.NameAz ?? "---",
                    HallName = order.Table?.Hall?.NameAz,
                    Description = $"Parçalı ödənişlərlə bağlandı. Yekun: {FormatAznAudit(payable)} ₼. ({payLineSplit})",
                    CompanyId = companyId
                });

                await transaction.CommitAsync();
                return null;
            }

            await transaction.CommitAsync();
            return await GetActiveOrderContentsAsync(order.TableId, companyId);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task TransferTableAsync(Guid orderId, Guid targetTableId, Guid companyId)
    {
        var order = await _context.OrderHeaders
            .Include(o => o.Table)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.CompanyId == companyId && !o.IsClosed);

        if (order == null)
            throw new Exception("Köçürüləcək aktiv sifariş tapılmadı!");

        var isTargetOccupied = await _context.OrderHeaders
            .AnyAsync(o => o.TableId == targetTableId && !o.IsClosed && o.CompanyId == companyId);

        if (isTargetOccupied)
            throw new Exception("Seçdiyiniz hədəf masa hazırda doludur!");

        var sourceTableName = order.Table?.NameAz ?? string.Empty;

        order.TableId = targetTableId;
        order.LastModifiedAt = AzTime;

        _context.OrderHeaders.Update(order);
        await _context.SaveChangesAsync();

        var targetTable = await _context.Tables.FindAsync(targetTableId);
        await _auditLogService.LogActionAsync(new AuditLogPostDto
        {
            UserName = order.CreatedBy,
            Action = "MASA KÖÇÜRÜLDÜ 🔄",
            TableName = sourceTableName,
            Description = $"Sifariş {sourceTableName} masasından {targetTable?.NameAz} masasına köçürüldü.",
            CompanyId = companyId
        });
    }

    public async Task<(Guid SourceOrderId, Guid TargetOrderId)> TransferOrderItemAsync(
        Guid sourceDetailId,
        Guid targetTableId,
        double quantity,
        Guid companyId)
    {
        if (quantity <= 0) throw new Exception("Miqdar düzgün deyil.");

        // source detail + order
        var src = await _context.OrderDetails
            .Include(d => d.OrderHeader)
            .FirstOrDefaultAsync(d => d.Id == sourceDetailId && d.CompanyId == companyId);

        if (src == null) throw new Exception("Məhsul tapılmadı.");
        if (src.OrderHeader == null) throw new Exception("Çek tapılmadı.");
        if (src.OrderHeader.IsClosed) throw new Exception("Bağlı çeki transfer etmək olmaz.");

        var srcQty = src.Quantity;
        if (quantity > srcQty) throw new Exception("Transfer miqdarı mövcud miqdardan böyük ola bilməz.");

        // target active order (yoxdursa avtomatik aç)
        var tgtOrder = await _context.OrderHeaders
            .FirstOrDefaultAsync(o => o.CompanyId == companyId && !o.IsClosed && o.TableId == targetTableId);

        if (tgtOrder == null)
        {
            var targetTable = await _context.Tables
                .Include(t => t.Hall)
                .FirstOrDefaultAsync(t => t.Id == targetTableId && t.CompanyId == companyId);

            if (targetTable == null) throw new Exception("Hədəf masa tapılmadı.");

            tgtOrder = new OrderHeader
            {
                Id = Guid.NewGuid(),
                TableId = targetTableId,
                CompanyId = companyId,
                OpenTime = AzTime,
                CheckNumber = $"CH-{AzTime:yyyyMMddHHmm}",
                IsClosed = false,
                ServicePercentage = targetTable.Hall?.ServicePercentage ?? 0,
                DepositAmount = targetTable.DepositAmount ?? 0,
                DepositStartTime = targetTable.DepositStartTime,
                DepositEndTime = targetTable.DepositEndTime,
                TotalAmount = targetTable.DepositAmount ?? 0,
                CreatedBy = src.CreatedBy ?? "Admin",
                CreatedAt = AzTime,
                LastModifiedAt = AzTime
            };

            targetTable.Status = TableStatus.Occupied;
            _context.Tables.Update(targetTable);
            await _context.OrderHeaders.AddAsync(tgtOrder);
            await _context.SaveChangesAsync();
        }

        if (tgtOrder.Id == src.OrderHeaderId)
            throw new Exception("Hədəf masa eyni çekdir.");

        var sourceOrderHeaderId = src.OrderHeaderId;
        var sourceLineWasSent = src.IsSent;

        using var tx = await _context.Database.BeginTransactionAsync();

        // 1) decrement source
        src.Quantity = Math.Max(0, src.Quantity - quantity);
        src.TotalPrice = (decimal)src.Quantity * src.Price;
        // Tam köçürdükdə sıfır sətir DB-də qalmasın (terminalda "0" görünməsin).
        if (src.Quantity <= 0)
            _context.OrderDetails.Remove(src);
        // Qayda: "Məhsul böl" əməliyyatı mətbəx statusunu avtomatik pozmasın.
        // Əgər məhsul əvvəldən mətbəxə göndərilibsə (IsSent=true), transfer bunu "unsent" etməməlidir.

        // 2) increment or create target line
        var tgtLine = await _context.OrderDetails.FirstOrDefaultAsync(d =>
            d.CompanyId == companyId &&
            d.OrderHeaderId == tgtOrder.Id &&
            d.ProductId == src.ProductId &&
            d.ProductVariantId == src.ProductVariantId &&
            d.Price == src.Price &&
            (d.ItemNote ?? "") == (src.ItemNote ?? "") &&
            (d.ProductName ?? "") == (src.ProductName ?? ""));

        if (tgtLine == null)
        {
            tgtLine = new OrderDetail
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                OrderHeaderId = tgtOrder.Id,
                ProductId = src.ProductId,
                ProductVariantId = src.ProductVariantId,
                ProductVariantName = src.ProductVariantName,
                ProductName = src.ProductName,
                Price = src.Price,
                Quantity = 0,
                ItemNote = src.ItemNote,
                TotalPrice = 0,
                IsSent = sourceLineWasSent,
                SplitGroup = 0,
                CreatedAt = AzTime,
                CreatedBy = src.CreatedBy
            };
            await _context.OrderDetails.AddAsync(tgtLine);
        }

        tgtLine.Quantity += quantity;
        tgtLine.TotalPrice = (decimal)tgtLine.Quantity * tgtLine.Price;
        // Əgər mənbə sətir mətbəxə göndərilməyibsə, hədəf sətiri də "unsent" olsun (mətbəx düyməsi çıxsın).
        // Əks halda (mənbə sent idisə), statusu dəyişmirik.
        if (!sourceLineWasSent)
            tgtLine.IsSent = false;

        await _context.SaveChangesAsync();

        // Çek cəmlərini sətirlərə uyğun yenilə (sıfırlanan sətir çıxarılandan sonra).
        var srcHeader = await _context.OrderHeaders
            .Include(o => o.OrderDetails)
            .FirstOrDefaultAsync(o => o.Id == sourceOrderHeaderId && o.CompanyId == companyId);
        if (srcHeader != null)
            await RecalculateOrderTotal(srcHeader);

        var tgtHeader = await _context.OrderHeaders
            .Include(o => o.OrderDetails)
            .FirstOrDefaultAsync(o => o.Id == tgtOrder.Id && o.CompanyId == companyId);
        if (tgtHeader != null)
            await RecalculateOrderTotal(tgtHeader);

        await tx.CommitAsync();

        var logSourceHeader = await _context.OrderHeaders
            .AsNoTracking()
            .Include(o => o.Table)
            .ThenInclude(t => t.Hall)
            .FirstOrDefaultAsync(o => o.Id == sourceOrderHeaderId && o.CompanyId == companyId);
        var logTargetHeader = await _context.OrderHeaders
            .AsNoTracking()
            .Include(o => o.Table)
            .ThenInclude(t => t.Hall)
            .FirstOrDefaultAsync(o => o.Id == tgtOrder.Id && o.CompanyId == companyId);

        var srcTableName = logSourceHeader?.Table?.NameAz ?? "?";
        var tgtTableName = logTargetHeader?.Table?.NameAz ?? "?";
        var productLabel = src.ProductName ?? "?";

        await _auditLogService.LogActionAsync(new AuditLogPostDto
        {
            UserName = src.CreatedBy ?? "—",
            Action = "MƏHSUL MASAYA KÖÇÜRÜLDÜ",
            TableName = srcTableName,
            HallName = logSourceHeader?.Table?.Hall?.NameAz,
            Description = $"{productLabel} ({quantity}) {srcTableName} → {tgtTableName}",
            CompanyId = companyId
        });

        return (sourceOrderHeaderId, tgtOrder.Id);
    }

    public async Task<OrderHeaderGetDto> MergeOrdersAsync(Guid targetOrderId, Guid sourceOrderId, Guid companyId)
    {
        if (targetOrderId == sourceOrderId)
            throw new Exception("Eyni çeki özü ilə birləşdirmək olmaz!");

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var target = await _context.OrderHeaders
                .Include(o => o.OrderDetails)
                .Include(o => o.Table).ThenInclude(t => t.Hall)
                .FirstOrDefaultAsync(o => o.Id == targetOrderId && o.CompanyId == companyId && !o.IsClosed);

            var source = await _context.OrderHeaders
                .Include(o => o.OrderDetails)
                .Include(o => o.Table)
                .FirstOrDefaultAsync(o => o.Id == sourceOrderId && o.CompanyId == companyId && !o.IsClosed);

            if (target == null || source == null)
                throw new Exception("Hədəf və ya mənbə aktiv çeki tapılmadı!");

            if (!source.OrderDetails.Any())
                throw new Exception("Mənbə masasında birləşdiriləcək məhsul yoxdur!");

            var sourceTableName = source.Table?.NameAz ?? "?";
            var targetTableName = target.Table?.NameAz ?? "?";

            foreach (var detail in source.OrderDetails.ToList())
            {
                var note = detail.ItemNote ?? "";
                var existing = target.OrderDetails.FirstOrDefault(d =>
                    d.ProductId == detail.ProductId && (d.ItemNote ?? "") == note);

                if (existing != null)
                {
                    // Mətbəx tarixçəsi OrderDetailId + OrderHeaderId ilə bağlıdır: mənbə sətirinin KitchenOperations-lərini hədəf sətirə köçür.
                    var srcOps = await _context.KitchenOperations
                        .Where(k => k.OrderDetailId == detail.Id && k.CompanyId == companyId)
                        .ToListAsync();
                    foreach (var op in srcOps)
                    {
                        op.OrderDetailId = existing.Id;
                        op.OrderHeaderId = target.Id;
                    }

                    existing.Quantity += detail.Quantity;
                    existing.TotalPrice = (decimal)existing.Quantity * existing.Price;

                    var netSent = await _context.KitchenOperations
                        .Where(k => k.OrderDetailId == existing.Id && k.OrderHeaderId == target.Id && k.CompanyId == companyId)
                        .SumAsync(x => (double?)x.Quantity) ?? 0d;
                    existing.IsSent = netSent >= existing.Quantity - 0.0001;

                    _context.OrderDetails.Update(existing);
                    _context.OrderDetails.Remove(detail);
                }
                else
                {
                    foreach (var op in await _context.KitchenOperations
                                 .Where(k => k.OrderDetailId == detail.Id && k.CompanyId == companyId)
                                 .ToListAsync())
                        op.OrderHeaderId = target.Id;

                    detail.OrderHeaderId = target.Id;
                    var netD = await _context.KitchenOperations
                        .Where(k => k.OrderDetailId == detail.Id && k.OrderHeaderId == target.Id && k.CompanyId == companyId)
                        .SumAsync(x => (double?)x.Quantity) ?? 0d;
                    detail.IsSent = netD >= detail.Quantity - 0.0001;
                    _context.OrderDetails.Update(detail);
                }
            }

            target.DepositAmount += source.DepositAmount;
            target.BehAmount += source.BehAmount;
            target.LastModifiedAt = AzTime;

            var sourceTableId = source.TableId;
            _context.OrderHeaders.Remove(source);

            var sourceTable = await _context.Tables.FirstOrDefaultAsync(t => t.Id == sourceTableId && t.CompanyId == companyId);
            if (sourceTable != null)
            {
                sourceTable.Status = TableStatus.Empty;
                _context.Tables.Update(sourceTable);
            }

            await RecalculateOrderTotal(target);

            await _auditLogService.LogActionAsync(new AuditLogPostDto
            {
                UserName = target.CreatedBy,
                Action = "ÇEKLƏR BİRLƏŞDİRİLDİ",
                TableName = targetTableName,
                Description = $"{sourceTableName} masasının çeki {targetTableName} masasına birləşdirildi; {sourceTableName} boşaldıldı.",
                CompanyId = companyId
            });

            await transaction.CommitAsync();
            return await GetActiveOrderContentsAsync(target.TableId, companyId);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<OrderHeaderGetDto> LinkOrderCustomerAsync(Guid orderId, Guid? customerId, Guid companyId)
    {
        var order = await _context.OrderHeaders
            .Include(o => o.Table).ThenInclude(t => t.Hall)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.CompanyId == companyId && !o.IsClosed)
            ?? throw new Exception("Sifariş tapılmadı və ya bağlanıb.");

        if (customerId.HasValue && customerId.Value != Guid.Empty)
        {
            var ok = await _context.Customers.AnyAsync(c =>
                c.Id == customerId.Value && c.CompanyId == companyId && !c.IsDeleted);
            if (!ok)
                throw new Exception("Müştəri tapılmadı.");
            order.CustomerId = customerId.Value;
        }
        else
        {
            order.CustomerId = null;
        }

        _context.OrderHeaders.Update(order);
        await _context.SaveChangesAsync();

        return await GetActiveOrderContentsAsync(order.TableId, companyId);
    }

    public async Task<List<string>> GetRecentItemNotesAsync(Guid companyId, int take = 10)
    {
        // ToUpperInvariant SQL-də tərcümə olunmur — əvvəl sadə sütun, sonra yaddaşda qruplaşdırma.
        // Set tərkibi tipli qeydlər (…) tez seçimdə olmasın; qalanlardan son istifadə üzrə TOP.
        static bool HasParenPair(string s) => s.Contains('(') && s.Contains(')');

        var rows = await _context.OrderDetails
            .AsNoTracking()
            .Where(d => d.CompanyId == companyId && d.ItemNote != null && d.ItemNote.Trim() != string.Empty)
            .Select(d => new { Note = d.ItemNote!.Trim(), d.CreatedAt })
            .ToListAsync();

        return rows
            .Where(x => !HasParenPair(x.Note))
            .GroupBy(x => x.Note.ToUpperInvariant())
            .Select(g => new
            {
                Sample = g.OrderByDescending(x => x.CreatedAt).First().Note,
                LastAt = g.Max(x => x.CreatedAt),
            })
            .OrderByDescending(x => x.LastAt)
            .Take(take)
            .Select(x => x.Sample)
            .ToList();
    }

    public async Task<List<OrderJournalEntryDto>> GetOrderJournalAsync(Guid orderId, Guid companyId)
    {
        var order = await _context.OrderHeaders
            .Include(o => o.Table)
            .ThenInclude(t => t.Hall)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId && o.CompanyId == companyId);

        if (order == null)
            throw new Exception("Sifariş tapılmadı!");

        var tableName = (order.Table?.NameAz ?? "").Trim();
        if (string.IsNullOrEmpty(tableName))
            throw new Exception("Masa adı tapılmadı.");

        var windowEnd = order.CloseTime?.AddMinutes(15)
            ?? DateTime.SpecifyKind(DateTime.UtcNow.AddHours(4).AddDays(1), DateTimeKind.Unspecified);

        // EF Core: string.Equals(..., StringComparison) SQL-ə çevrilmir — ToLower() ilə müqayisə.
        var tableKey = tableName.ToLowerInvariant();
        var logs = await _context.AuditLogs
            .AsNoTracking()
            .Where(l => l.CompanyId == companyId)
            .Where(l => l.CreatedAt >= order.OpenTime.AddSeconds(-60) && l.CreatedAt <= windowEnd)
            .Where(l => l.TableName != null && l.TableName.Trim().ToLower() == tableKey)
            .OrderBy(l => l.CreatedAt)
            .ToListAsync();

        var hallAz = order.Table?.Hall?.NameAz;
        var list = new List<OrderJournalEntryDto>
        {
            new()
            {
                At = order.OpenTime,
                Kind = "open",
                Title = "Çek açıldı",
                // Jurnalda masa adı göstərilmir; yalnız zal adı (varsa) əlavə kontekst üçün.
                Detail = string.IsNullOrWhiteSpace(hallAz) ? null : hallAz.Trim(),
                UserName = string.IsNullOrWhiteSpace(order.CreatedBy) ? null : order.CreatedBy.Trim(),
            },
        };

        foreach (var l in logs)
        {
            var action = (l.Action ?? "").Trim();
            if (action.Contains("SİFARİŞ AÇILDI", StringComparison.OrdinalIgnoreCase))
                continue;

            list.Add(new OrderJournalEntryDto
            {
                At = l.CreatedAt,
                Kind = "audit",
                Title = action,
                Detail = string.IsNullOrWhiteSpace(l.Description) ? null : l.Description.Trim(),
                UserName = string.IsNullOrWhiteSpace(l.UserName) ? null : l.UserName.Trim(),
            });
        }

        return list.OrderBy(x => x.At).ToList();
    }
}