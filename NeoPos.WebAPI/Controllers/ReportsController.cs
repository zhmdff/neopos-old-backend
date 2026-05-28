using BusinessLayer.Services.Abstractions;
using BusinessLayer.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace NeoPos.WebAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService) => _reportService = reportService;

    /// <summary>
    /// DB-də CloseTime Bakı divar saatı kimi saxlanır; query ISO (+04:00 / Z) ola bilər — əvvəl Bakı müqayisəsinə salınır.
    /// İki parametrdə də 00:00-dırsa: inclusive təqvim günü (köhnə Boss «start=end=tarix»).
    /// Eyni təqvim günü 00:00–23:59: tam gün (terminal tarix seçicisi; əks halda 23:59:01+ çeklər çıxmayır).
    /// </summary>
    private static (DateTime Start, DateTime End) NormalizeRange(DateTime start, DateTime end)
    {
        start = ReportQueryBakuTime.ToBakuWallForDbComparison(start);
        end = ReportQueryBakuTime.ToBakuWallForDbComparison(end);

        var startMid = start.TimeOfDay == TimeSpan.Zero;
        var endMid = end.TimeOfDay == TimeSpan.Zero;
        if (startMid && endMid)
            return (start.Date, end.Date.AddDays(1).AddTicks(-1));

        if (start.Date == end.Date && startMid && end.TimeOfDay >= new TimeSpan(23, 59, 0))
            return (start.Date, end.Date.AddDays(1).AddTicks(-1));

        return (start, end);
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] DateTime start,
        [FromQuery] DateTime end,
        [FromQuery] Guid companyId,
        [FromQuery] bool includeOpenTables = false)
    {
        if (companyId == Guid.Empty) return BadRequest("Şirkət ID-si mütləqdir!");

        var (s, e) = NormalizeRange(start, end);
        var result = await _reportService.GetGeneralSummaryAsync(s, e, companyId, includeOpenTables);
        return Ok(result);
    }

    [HttpGet("by-table")]
    public async Task<IActionResult> GetByTable(
        [FromQuery] DateTime start,
        [FromQuery] DateTime end,
        [FromQuery] Guid companyId,
        [FromQuery] bool includeOpenTables = false)
    {
        if (companyId == Guid.Empty) return BadRequest("Şirkət ID-si mütləqdir!");
        var (s, e) = NormalizeRange(start, end);
        var result = await _reportService.GetTableBreakdownAsync(s, e, companyId, includeOpenTables);
        return Ok(result);
    }

    [HttpGet("by-waiter")]
    public async Task<IActionResult> GetByWaiter(
        [FromQuery] DateTime start,
        [FromQuery] DateTime end,
        [FromQuery] Guid companyId,
        [FromQuery] bool includeOpenTables = false)
    {
        if (companyId == Guid.Empty) return BadRequest("Şirkət ID-si mütləqdir!");
        var (s, e) = NormalizeRange(start, end);
        var result = await _reportService.GetWaiterBreakdownAsync(s, e, companyId, includeOpenTables);
        return Ok(result);
    }

    [HttpGet("by-workshop")]
    public async Task<IActionResult> GetByWorkshop(
        [FromQuery] DateTime start,
        [FromQuery] DateTime end,
        [FromQuery] Guid companyId,
        [FromQuery] bool includeOpenTables = false)
    {
        if (companyId == Guid.Empty) return BadRequest("Şirkət ID-si mütləqdir!");
        var (s, e) = NormalizeRange(start, end);
        var result = await _reportService.GetWorkshopBreakdownAsync(s, e, companyId, includeOpenTables);
        return Ok(result);
    }

    [HttpGet("by-product")]
    public async Task<IActionResult> GetByProduct(
        [FromQuery] DateTime start,
        [FromQuery] DateTime end,
        [FromQuery] Guid companyId,
        [FromQuery] int take = 50,
        [FromQuery] bool includeOpenTables = false)
    {
        if (companyId == Guid.Empty) return BadRequest("Şirkət ID-si mütləqdir!");
        var (s, e) = NormalizeRange(start, end);
        var result = await _reportService.GetProductBreakdownAsync(s, e, companyId, take, includeOpenTables);
        return Ok(result);
    }

    [HttpGet("shift/{shiftId}/by-product")]
    public async Task<IActionResult> GetShiftByProduct(
        Guid shiftId,
        [FromQuery] Guid companyId,
        [FromQuery] int take = 50,
        [FromQuery] bool includeOpenTables = false)
    {
        if (companyId == Guid.Empty) return BadRequest("Şirkət ID-si mütləqdir!");

        try
        {
            var result = await _reportService.GetShiftProductBreakdownAsync(shiftId, companyId, take, includeOpenTables);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("shift/{shiftId}/by-waiter")]
    public async Task<IActionResult> GetShiftByWaiter(
        Guid shiftId,
        [FromQuery] Guid companyId,
        [FromQuery] bool includeOpenTables = false)
    {
        if (companyId == Guid.Empty) return BadRequest("Şirkət ID-si mütləqdir!");

        try
        {
            var result = await _reportService.GetShiftWaiterBreakdownAsync(shiftId, companyId, includeOpenTables);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("shift/{shiftId}/by-workshop")]
    public async Task<IActionResult> GetShiftByWorkshop(
        Guid shiftId,
        [FromQuery] Guid companyId,
        [FromQuery] bool includeOpenTables = false)
    {
        if (companyId == Guid.Empty) return BadRequest("Şirkət ID-si mütləqdir!");

        try
        {
            var result = await _reportService.GetShiftWorkshopBreakdownAsync(shiftId, companyId, includeOpenTables);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("shift/{shiftId}/by-table")]
    public async Task<IActionResult> GetShiftByTable(
        Guid shiftId,
        [FromQuery] Guid companyId,
        [FromQuery] bool includeOpenTables = false)
    {
        if (companyId == Guid.Empty) return BadRequest("Şirkət ID-si mütləqdir!");

        try
        {
            var result = await _reportService.GetShiftTableBreakdownAsync(shiftId, companyId, includeOpenTables);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("customers-loyalty")]
    public async Task<IActionResult> GetCustomersLoyalty(
        [FromQuery] DateTime start,
        [FromQuery] DateTime end,
        [FromQuery] Guid companyId,
        [FromQuery] string? q = null,
        [FromQuery] int take = 200)
    {
        if (companyId == Guid.Empty) return BadRequest("Şirkət ID-si mütləqdir!");
        var (s, e) = NormalizeRange(start, end);
        var result = await _reportService.GetCustomerLoyaltyAsync(s, e, companyId, q, take);
        return Ok(result);
    }

    [HttpGet("shift/{shiftId}")]
    public async Task<IActionResult> GetShiftReport(
        Guid shiftId,
        [FromQuery] Guid companyId,
        [FromQuery] bool includeOpenTables = false)
    {
        if (companyId == Guid.Empty) return BadRequest("Şirkət ID-si mütləqdir!");

        try
        {
            return Ok(await _reportService.GetShiftReportAsync(shiftId, companyId, includeOpenTables));
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("all-shifts")]
    public async Task<IActionResult> GetAllShifts([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] Guid companyId = default)
    {
        if (companyId == Guid.Empty)
        {
            return BadRequest("Şirkət ID-si göndərilməlidir!");
        }

        var (items, totalCount) = await _reportService.GetAllShiftsAsync(page, pageSize, companyId);

        return Ok(new { items, totalCount });
    }
}