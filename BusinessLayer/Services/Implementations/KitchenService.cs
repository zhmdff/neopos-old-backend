using BusinessLayer.DTOs.Audit;
using BusinessLayer.DTOs.Kitchen;
using BusinessLayer.Hubs;
using BusinessLayer.Printing;
using BusinessLayer.Services.Abstractions;
using BusinessLayer.Utilities;
using DAL.Server.Context;
using Domain.Common.Entities;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BusinessLayer.Services.Implementations;

public class KitchenService : IKitchenService
{
    private readonly AppDbContext _context;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly IAuditLogService _auditLogService;
    private readonly ITcpPrinterService _tcpPrinterService;

    public KitchenService(
        AppDbContext context,
        IHubContext<NotificationHub> hubContext,
        IAuditLogService auditLogService,
        ITcpPrinterService tcpPrinterService)
    {
        _context = context;
        _hubContext = hubContext;
        _auditLogService = auditLogService;
        _tcpPrinterService = tcpPrinterService;
    }

    /// <summary>
    /// Mətbəxə göndərilən say həmişə sətirdəki miqdardır.
    /// Qiymət dəyişimi porsiyaya çevrilməməlidir (məs. endirim → mətbəxdə 1.5 pors kimi çıxmasın).
    /// </summary>
    private static double GetKitchenEffectiveQuantity(OrderDetail d) => d.Quantity;

    public async Task<List<KitchenPrintGroupDto>> ProcessKitchenDeltaAsync(
        Guid orderHeaderId,
        Guid companyId,
        bool broadcastPrintToTerminals = false,
        bool flushPending = true)
    {
        // 1. Təhlükəsizlik yoxlaması
        var orderHeader = await _context.OrderHeaders
            .AsNoTracking()
            .Include(oh => oh.Table)
            .ThenInclude(t => t.Hall)
            .FirstOrDefaultAsync(oh => oh.Id == orderHeaderId && oh.CompanyId == companyId);

        if (orderHeader == null) throw new Exception("Sifariş tapılmadı!");

        // 2. Detalları və Sexləri çəkirik
        var details = await _context.OrderDetails
            .Include(x => x.Product).ThenInclude(p => p.Workshop)
            .Include(x => x.Product).ThenInclude(p => p.AdditionalWorkshops).ThenInclude(x => x.Workshop)
            .Include(x => x.Product).ThenInclude(p => p.Variants)
            .Where(x => x.OrderHeaderId == orderHeaderId && x.CompanyId == companyId)
            .ToListAsync();

        var previousOps = await _context.KitchenOperations
            .Where(x => x.OrderHeaderId == orderHeaderId && x.CompanyId == companyId)
            .ToListAsync();

        var currentOps = new List<KitchenOperation>();

        // YENİ VƏ YA DƏYİŞMİŞ MƏHSULLAR
        foreach (var detail in details)
        {
            var sentQty = previousOps.Where(x => x.OrderDetailId == detail.Id).Sum(x => x.Quantity);
            var effectiveQty = GetKitchenEffectiveQuantity(detail);
            var diff = effectiveQty - sentQty;

            var lastSentNote = previousOps
                .Where(x => x.OrderDetailId == detail.Id)
                .OrderByDescending(x => x.SentAt)
                .Select(x => x.Note).FirstOrDefault() ?? "";

            // Qeyd təkrarlanmasın deyə: son göndərilən QEYD-i (boş olmayan) tapırıq
            var lastSentNonEmptyNote = previousOps
                .Where(x => x.OrderDetailId == detail.Id && !string.IsNullOrWhiteSpace(x.Note))
                .OrderByDescending(x => x.SentAt)
                .Select(x => x.Note).FirstOrDefault() ?? "";

            var combinedNote = KitchenLineNotes.CombinedForKitchen(detail);
            bool noteChanged = combinedNote != lastSentNonEmptyNote.Trim();

            // flushPending=false: mətbəxə heç vaxt göndərilməmiş sətirə toxunmuruq; +miqdar gözləyir, çap olmamalıdır.
            if (!flushPending && sentQty <= 0)
                continue;
            if (!flushPending && diff > 0)
                continue;

            if (diff != 0 || noteChanged)
            {
                var baseName = string.IsNullOrWhiteSpace(detail.ProductName) ? detail.Product?.NameAz : detail.ProductName;
                var variantName = detail.ProductVariantName;
                var displayName = baseName;
                if (!string.IsNullOrWhiteSpace(variantName))
                {
                    // Bəzi köhnə datalarda ProductName yalnız məhsul adıdır — variantı əlavə edək.
                    var bn = (baseName ?? "").Trim();
                    var vn = variantName.Trim();
                    if (!string.IsNullOrWhiteSpace(bn) && !string.IsNullOrWhiteSpace(vn))
                    {
                        var containsVariant =
                            bn.Contains(vn, StringComparison.InvariantCultureIgnoreCase) ||
                            bn.Contains($"{bn} - {vn}", StringComparison.InvariantCultureIgnoreCase);
                        if (!containsVariant) displayName = $"{bn} - {vn}";
                    }
                }

                currentOps.Add(new KitchenOperation
                {
                    Id = Guid.NewGuid(),
                    OrderHeaderId = orderHeaderId,
                    OrderDetailId = detail.Id,
                    // Variant daxil olmaqla real çekdəki ad (məs: "Çörək - 150 qr")
                    ProductName = displayName ?? detail.Product?.NameAz ?? "",
                    Quantity = diff,
                    // Qeyd yalnız dəyişəndə çap olunsun; əks halda boş saxla (birləşmiş — KitchenOperation audit)
                    Note = noteChanged ? combinedNote : "",
                    OperationType = diff > 0 ? KitchenOperationType.New : KitchenOperationType.Reduced,
                    SentAt = DateTime.UtcNow,
                    CompanyId = companyId
                });
                detail.IsSent = true;
            }
        }

        // SİLİNMƏNİ TUTMAQ
        var deletedIds = previousOps.Select(x => x.OrderDetailId).Distinct()
            .Where(id => !details.Any(d => d.Id == id)).ToList();

        foreach (var oldId in deletedIds)
        {
            var alreadySent = previousOps.Where(x => x.OrderDetailId == oldId).Sum(x => x.Quantity);
            if (alreadySent > 0)
            {
                var firstOp = previousOps.First(x => x.OrderDetailId == oldId);
                currentOps.Add(new KitchenOperation
                {
                    Id = Guid.NewGuid(),
                    OrderHeaderId = orderHeaderId,
                    OrderDetailId = oldId,
                    ProductName = firstOp.ProductName,
                    Quantity = -alreadySent,
                    OperationType = KitchenOperationType.Cancelled,
                    SentAt = DateTime.UtcNow,
                    CompanyId = companyId,
                    Note = "SİLİNDİ"
                });
            }
        }

        if (!currentOps.Any()) return new List<KitchenPrintGroupDto>();

        _context.KitchenOperations.AddRange(currentOps);
        await _context.SaveChangesAsync();

        // Masa tarixçəsi (çek jurnalı): AuditLogs — GetOrderJournalAsync masa adı + vaxt pəncərəsi ilə göstərir.
        var tableAzForAudit = (orderHeader.Table?.NameAz ?? "").Trim();
        if (!string.IsNullOrEmpty(tableAzForAudit))
        {
            try
            {
                var who = (orderHeader.WaiterName ?? orderHeader.CreatedBy ?? "—").Trim();
                if (string.IsNullOrEmpty(who)) who = "—";
                var descParts = new List<string>();
                foreach (var op in currentOps)
                {
                    var st = op.OperationType == KitchenOperationType.New ? "YENİ" :
                        op.OperationType == KitchenOperationType.Reduced ? "AZALDI" : "LƏĞV";
                    var nm = (op.ProductName ?? "").Trim();
                    if (string.IsNullOrEmpty(nm)) nm = "—";
                    descParts.Add($"{nm} × {Math.Abs(op.Quantity):0.###} ({st})");
                }

                var summary = descParts.Count > 0 ? string.Join("; ", descParts) : "—";
                if (summary.Length > 900) summary = summary[..900] + "…";

                await _auditLogService.LogActionAsync(new AuditLogPostDto
                {
                    UserId = Guid.Empty,
                    UserName = who,
                    CreatedBy = who,
                    Action = "MƏTBƏX ÇAPI",
                    TableName = tableAzForAudit,
                    HallName = orderHeader.Table?.Hall?.NameAz,
                    Description = $"Mətbəxə çap göndərildi. {summary}",
                    CompanyId = companyId
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Kitchen audit log: {ex.Message}");
            }
        }

        // --- KRİTİK DÜZƏLİŞ: Çap qeyd olunduqdan sonra miqdarı 0 olanları təmizləyirik ---
        var zeroItems = details.Where(x => x.Quantity <= 0).ToList();
        if (zeroItems.Any())
        {
            _context.OrderDetails.RemoveRange(zeroItems);
            await _context.SaveChangesAsync();
        }

        // 4. Qruplaşdırma və DTO hazırlığı
        var productMap = details.ToDictionary(x => x.Id, x => x);

        // Multi-workshop: hər məhsulu primary + əlavə sexlərə paylayırıq
        var groupsByWorkshopId = new Dictionary<Guid, (Workshop Ws, List<KitchenPrintItemDto> Items)>();
        var fallbackItems = new List<KitchenPrintItemDto>();

        foreach (var op in currentOps)
        {
            var sentQtyBefore = previousOps.Where(x => x.OrderDetailId == op.OrderDetailId).Sum(x => x.Quantity);

            if (!productMap.TryGetValue(op.OrderDetailId, out var d) || d.Product == null)
            {
                fallbackItems.Add(KitchenLineNotes.ToPrintItem(op, null, sentQtyBefore, 0));
                continue;
            }

            var workshops = new List<Workshop>();
            if (d.Product.Workshop != null) workshops.Add(d.Product.Workshop);
            if (d.Product.AdditionalWorkshops != null && d.Product.AdditionalWorkshops.Count > 0)
            {
                workshops.AddRange(d.Product.AdditionalWorkshops
                    .Where(x => x.Workshop != null && x.WorkshopId != d.Product.WorkshopId)
                    .Select(x => x.Workshop));
            }

            // Dedup by Id
            workshops = workshops
                .GroupBy(w => w.Id)
                .Select(g => g.First())
                .ToList();

            var printItem = KitchenLineNotes.ToPrintItem(op, d, sentQtyBefore, GetKitchenEffectiveQuantity(d));

            if (workshops.Count == 0)
            {
                fallbackItems.Add(printItem);
                continue;
            }

            foreach (var ws in workshops)
            {
                if (!groupsByWorkshopId.TryGetValue(ws.Id, out var slot))
                {
                    slot = (ws, new List<KitchenPrintItemDto>());
                    groupsByWorkshopId[ws.Id] = slot;
                }
                slot.Items.Add(printItem);
            }
        }

        var groups = groupsByWorkshopId.Values
            .Select(g => new KitchenPrintGroupDto
            {
                WorkshopName = g.Ws?.NameAz ?? "Mətbəx",
                PrinterType = g.Ws?.PrinterType ?? "Network",
                PrinterValue = g.Ws?.PrinterValue ?? "9100",
                Items = g.Items
            })
            .ToList();

        if (fallbackItems.Count > 0)
        {
            groups.Add(new KitchenPrintGroupDto
            {
                WorkshopName = "Mətbəx",
                PrinterType = "Network",
                PrinterValue = "9100",
                Items = fallbackItems
            });
        }

        var tableName = orderHeader.Table?.NameAz ?? "";
        var hallName = orderHeader.Table?.Hall?.NameAz ?? "";

        var company = await _context.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == companyId);

        // --- DIRECT BACKEND PRINTING (LAN) ---
        foreach (var group in groups)
        {
            if (group.PrinterType?.Equals("Network", StringComparison.OrdinalIgnoreCase) == true)
            {
                try
                {
                    if (!PrinterTargetParser.TryParseNetworkTarget(group.PrinterValue, out var ip, out var port, out var beepMode))
                        continue;

                    var bytes = _tcpPrinterService.GenerateKitchenEscPos(
                        company?.ReceiptDesignSettingsJson,
                        group.WorkshopName,
                        hallName,
                        tableName,
                        orderHeader.WaiterName ?? "",
                        orderHeader.OpenTime,
                        group.Items,
                        beepMode);

                    await _tcpPrinterService.SendToPrinterAsync(ip, port, bytes);
                    group.PrinterType = "BackendHandled";
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Direct Print Error for {group.WorkshopName}: {ex.Message}");
                }
            }
        }

        var broadcastGroups = groups.Where(g => g.PrinterType != "BackendHandled").ToList();

        if (broadcastPrintToTerminals && broadcastGroups.Count > 0)
        {
            try
            {
                var groupKey = companyId.ToString("D").ToLowerInvariant();
                await _hubContext.Clients.Group(groupKey)
                    .SendAsync("KitchenPrintJob", new
                    {
                        orderHeaderId,
                        tableName,
                        hallName,
                        waiterName = orderHeader.WaiterName ?? "",
                        openTime = orderHeader.OpenTime.ToString("o"),
                        groups = broadcastGroups
                    });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"KitchenPrintJob SignalR: {ex.Message}");
            }
        }

        return groups;
    }
}