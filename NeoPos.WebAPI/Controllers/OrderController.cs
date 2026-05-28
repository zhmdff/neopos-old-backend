using BusinessLayer.DTOs.Kitchen;
using BusinessLayer.DTOs.OrderDetail;
using BusinessLayer.DTOs.OrderHeader;
using BusinessLayer.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace NeoPos.WebAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly IKitchenService _kitchenService;

    public OrdersController(IOrderService orderService, IKitchenService kitchenService)
    {
        _orderService = orderService;
        _kitchenService = kitchenService;
    }

    [HttpPost("open")]
    public async Task<IActionResult> OpenOrder([FromBody] OrderHeaderPostDto dto)
    {
        // DTO daxilində CompanyId mütləq olmalıdır
        return Ok(await _orderService.OpenOrderAsync(dto));
    }

    [HttpGet("active/{tableId}")]
    public async Task<IActionResult> GetActiveOrder(Guid tableId, [FromQuery] Guid companyId)
    {
        var result = await _orderService.GetActiveOrderContentsAsync(tableId, companyId);
        return Ok(result);
    }

    /// <summary>Terminal: aktiv çek üzrə audit xronologiyası (açılış, məhsul, silinmə və s.).</summary>
    [HttpGet("{orderId:guid}/journal")]
    public async Task<IActionResult> GetOrderJournal(Guid orderId, [FromQuery] Guid companyId)
    {
        try
        {
            return Ok(await _orderService.GetOrderJournalAsync(orderId, companyId));
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("add-items/{orderId}")]
    public async Task<IActionResult> AddItems(Guid orderId, [FromBody] List<OrderDetailPostDto> items, [FromQuery] Guid companyId)
    {
        return Ok(await _orderService.AddItemsToOrderAsync(orderId, items, companyId));
    }

    [HttpPut("items/{detailId}")]
    public async Task<IActionResult> UpdateItem(Guid detailId, [FromBody] OrderDetailUpdateDto dto, [FromQuery] Guid companyId)
    {
        dto.CompanyId = companyId;

        var result = await _orderService.UpdateOrderItemAsync(detailId, dto);

        return Ok(result);
    }

    [HttpPut("{orderId:guid}/guest-count")]
    public async Task<IActionResult> UpdateGuestCount(Guid orderId, [FromBody] OrderGuestCountPutDto dto, [FromQuery] Guid companyId)
    {
        return Ok(await _orderService.UpdateOrderGuestCountAsync(orderId, dto.GuestCount, companyId));
    }

    [HttpPut("{orderId:guid}/table-hour-bonus")]
    public async Task<IActionResult> UpdateTableHourBonus(Guid orderId, [FromBody] OrderTableHourBonusPutDto dto, [FromQuery] Guid companyId)
    {
        return Ok(await _orderService.UpdateTableHourBonusAsync(orderId, dto.TableHourBonusMinutes, companyId));
    }

    [HttpDelete("items/{detailId}")]
    public async Task<IActionResult> RemoveItem(Guid detailId, [FromQuery] Guid companyId, [FromQuery] string? reason = null)
    {
        return Ok(await _orderService.RemoveOrderItemAsync(detailId, companyId, reason));
    }

    [HttpPut("{orderId}/update-service-fee")]
    public async Task<IActionResult> UpdateServiceFee(Guid orderId, [FromBody] decimal newPercentage, [FromQuery] Guid companyId)
    {
        var result = await _orderService.UpdateServiceFeeAsync(orderId, newPercentage, companyId);
        return Ok(result);
    }

    [HttpDelete("{orderId}")]
    public async Task<IActionResult> DeleteOrder(Guid orderId, [FromQuery] Guid companyId)
    {
        try
        {
            var result = await _orderService.DeleteOrderAsync(orderId, companyId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            // Oflayn növbə: 500 əvəzinə 400 — klient köhnə DELETE-i növbədən silə bilsin
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{orderId}/update-deposit")]
    public async Task<IActionResult> UpdateDeposit(Guid orderId, [FromBody] decimal amount, [FromQuery] Guid companyId)
    {
        var result = await _orderService.UpdateOrderDepositAsync(orderId, amount, null, null, companyId);
        if (result == null) return NotFound(); // Cursor yeniləmə etdi — oflayn növbə üçün 404
        return Ok(result);
    }

    [HttpPut("{orderId}/update-discount")]
    public async Task<IActionResult> UpdateDiscount(Guid orderId, [FromBody] DiscountUpdateDto dto, [FromQuery] Guid companyId)
    {
        try
        {
            var result = await _orderService.UpdateOrderDiscountAsync(orderId, dto.Value, dto.IsPercentage, companyId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{orderId}/update-beh")]
    public async Task<IActionResult> UpdateBeh(Guid orderId, [FromBody] decimal amount, [FromQuery] Guid companyId)
    {
        try
        {
            var result = await _orderService.UpdateOrderBehAsync(orderId, amount, companyId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{orderId}/update-note")]
    public async Task<IActionResult> UpdateNote(Guid orderId, [FromBody] NoteUpdateDto dto, [FromQuery] Guid companyId)
    {
        try
        {
            var result = await _orderService.UpdateOrderNoteAsync(orderId, dto.Note, companyId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{orderId}/change-waiter")]
    public async Task<IActionResult> ChangeWaiter(Guid orderId, [FromBody] OrderWaiterChangeDto dto, [FromQuery] Guid companyId)
    {
        try
        {
            var result = await _orderService.ChangeOrderWaiterAsync(orderId, dto?.FullName ?? "", companyId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{orderId}/customer")]
    public async Task<IActionResult> LinkCustomer(Guid orderId, [FromBody] OrderCustomerLinkDto dto, [FromQuery] Guid companyId)
    {
        try
        {
            var result = await _orderService.LinkOrderCustomerAsync(orderId, dto.CustomerId, companyId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("mark-as-sent")]
    public async Task<IActionResult> MarkAsSent([FromBody] MarkAsSentDto dto, [FromQuery] Guid companyId)
    {
        var result = await _orderService.MarkOrderItemsAsSentAsync(dto, companyId);
        if (result) return Ok();
        return BadRequest("Məhsullar tapılmadı və ya xəta baş verdi");
    }

    [HttpPost("close")]
    public async Task<IActionResult> CloseOrder([FromBody] OrderCloseDto dto, [FromQuery] Guid companyId)
    {
        try
        {
            if (dto.OrderId == Guid.Empty) return BadRequest("Sifariş ID-si mütləqdir!");
            var result = await _orderService.CloseOrderAsync(dto, companyId);
            if (result) return Ok(new { message = "Sifariş uğurla bağlandı." });
            return BadRequest("Sifarişi bağlamaq mümkün olmadı.");
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [Authorize]
    [HttpPost("{orderId}/reopen-from-shift-archive")]
    public async Task<IActionResult> ReopenFromShiftArchive(
        Guid orderId,
        [FromQuery] Guid companyId,
        [FromBody] ReopenArchiveOrderDto? body)
    {
        var claimCompany = User?.FindFirst("CompanyId")?.Value;
        if (!Guid.TryParse(claimCompany, out var jwtCompany) || jwtCompany != companyId)
            return BadRequest(new { message = "Şirkət uyğunsuzluğu." });

        var idClaim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(idClaim) || idClaim.StartsWith("waiter:", StringComparison.OrdinalIgnoreCase))
            return Unauthorized();

        if (!Guid.TryParse(idClaim, out var userId))
            return Unauthorized();

        try
        {
            var result = await _orderService.ReopenShiftArchiveOrderAsync(
                orderId,
                companyId,
                userId,
                body?.PresetKey,
                body?.Note);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("closed-orders")]
    public async Task<IActionResult> GetClosedOrders(
    [FromQuery] Guid companyId,
    [FromQuery] DateTime? date = null,
    [FromQuery] DateTime? startDate = null,
    [FromQuery] DateTime? endDate = null,
    [FromQuery] Guid? cashShiftId = null,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 10)
    {
        // Əgər start/end göndərilibsə, frontend-in tarix aralığı filtri kimi işləyir.
        // Əks halda köhnə `date` filtri (tək gün) saxlanılır.
        var result = await _orderService.GetClosedOrdersAsync(companyId, date, cashShiftId, page, pageSize, startDate, endDate);
        return Ok(result);
    }


    [HttpPost("process-kitchen/{orderId}")]
    public async Task<IActionResult> ProcessKitchen(
        Guid orderId,
        [FromQuery] Guid companyId,
        [FromQuery] bool broadcastPrint = false,
        [FromQuery] bool flushPending = true)
    {
        try
        {
            // KitchenService-ə də companyId ötürürük
            var result = await _kitchenService.ProcessKitchenDeltaAsync(orderId, companyId, broadcastPrint, flushPending);
            if (result == null || !result.Any())
                return Ok(new { message = "Heç bir dəyişiklik yoxdur", items = new List<object>() });

            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("transfer-table")]
    public async Task<IActionResult> TransferTable([FromBody] TableTransferDto dto, [FromQuery] Guid companyId)
    {
        try
        {
            // Service-ə birbaşa frontdan gələn companyId-ni ötürürük
            await _orderService.TransferTableAsync(dto.OrderId, dto.TargetTableId, companyId);
            return Ok(new { message = "Masa uğurla dəyişdirildi." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("transfer-item")]
    public async Task<IActionResult> TransferItem([FromBody] TransferOrderItemDto dto, [FromQuery] Guid companyId)
    {
        if (companyId == Guid.Empty) return BadRequest(new { message = "Şirkət ID-si mütləqdir." });
        if (dto.CompanyId != companyId) dto.CompanyId = companyId;
        try
        {
            var (sourceOrderId, targetOrderId) = await _orderService.TransferOrderItemAsync(
                dto.SourceDetailId,
                dto.TargetTableId,
                dto.Quantity,
                companyId);

            // Qayda: "Məhsul böl" əməliyyatı mətbəxə avtomatik təsir etməməlidir.
            // Burada ProcessKitchenDeltaAsync çağırsaq, bəzi hallarda sətirlər "sent" kimi işarələnir
            // və terminalda "Mətbəxə göndər" düyməsi itir. Ona görə yalnız transferi edirik.
            return Ok(new { sourceOrderId, targetOrderId });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{orderId}/splits")]
    public async Task<IActionResult> UpdateSplits(Guid orderId, [FromBody] UpdateOrderSplitsDto dto, [FromQuery] Guid companyId)
    {
        try
        {
            var result = await _orderService.UpdateOrderSplitAssignmentsAsync(orderId, dto, companyId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("pay-split")]
    public async Task<IActionResult> PaySplit([FromBody] PaySplitDto dto, [FromQuery] Guid companyId)
    {
        try
        {
            var result = await _orderService.PayOrderSplitAsync(dto, companyId);
            if (result == null) return Ok(new { closed = true, message = "Sifariş bağlandı." });
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("merge")]
    public async Task<IActionResult> MergeOrders([FromBody] MergeOrdersDto dto, [FromQuery] Guid companyId)
    {
        try
        {
            var result = await _orderService.MergeOrdersAsync(dto.TargetOrderId, dto.SourceOrderId, companyId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("recent-notes")]
    public async Task<IActionResult> GetRecentNotes([FromQuery] Guid companyId)
    {
        var notes = await _orderService.GetRecentItemNotesAsync(companyId);
        return Ok(notes);
    }


}