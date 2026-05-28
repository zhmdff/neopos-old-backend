using BusinessLayer.DTOs.Product;
using BusinessLayer.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;

[Route("api/[controller]")]
[ApiController]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly IAuditLogService _auditLogService;

    public ProductsController(IProductService productService, IAuditLogService auditLogService)
    {
        _productService = productService;
        _auditLogService = auditLogService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid companyId, // Əlavə olundu
        [FromQuery] int skip = 0,
        [FromQuery] int take = 10,
        [FromQuery] string? search = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] Guid? workshopId = null,
        [FromQuery] bool uncategorizedOnly = false)
    {
        var result = await _productService.GetAllAsync(companyId, skip, take, search, categoryId, workshopId, uncategorizedOnly);
        return Ok(result);
    }

    [HttpPost("update-orders")]
    public async Task<IActionResult> UpdateOrders([FromQuery] Guid companyId, [FromBody] List<ProductOrderUpdateDto> dtos)
    {
        try
        {
            await _productService.UpdateOrdersAsync(companyId, dtos);
            return Ok(new { message = "Məhsul sıralaması yeniləndi!" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromForm] ProductPostDto dto)
    {
        try
        {
            var id = await _productService.CreateAsync(dto);
            return Ok(new { id, message = "Məhsul uğurla əlavə edildi!" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromForm] ProductPutDto dto)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            await _productService.UpdateAsync(dto);
            return Ok(new { message = "Məhsul məlumatları yeniləndi!" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// JSON ilə yeniləmə (şəkil yox). Boss-da Tex kart və s. PUT application/json göndərirsə bu ünvanı istifadə edin: PUT .../api/Products/json
    /// </summary>
    [HttpPut("json")]
    public async Task<IActionResult> UpdateJson([FromBody] ProductPutJsonDto jsonDto)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        if (jsonDto == null)
            return BadRequest(new { message = "JSON gövdə boşdur." });

        var dto = new ProductPutDto
        {
            Id = jsonDto.Id,
            NameAz = jsonDto.NameAz ?? string.Empty,
            Barcode = jsonDto.Barcode,
            CostPrice = jsonDto.CostPrice,
            MarkupValue = jsonDto.MarkupValue,
            MarkupType = jsonDto.MarkupType,
            CookingProcess = jsonDto.CookingProcess,
            CategoryId = jsonDto.CategoryId,
            WorkshopId = jsonDto.WorkshopId,
            CompanyId = jsonDto.CompanyId,
            Unit = jsonDto.Unit,
            DeliveryPrice = jsonDto.DeliveryPrice,
            ShowInQr = jsonDto.ShowInQr,
            ShowInTerminal = jsonDto.ShowInTerminal,
            AdditionalWorkshopIds = jsonDto.AdditionalWorkshopIds ?? new List<Guid>(),
            ImageFile = null,
        };

        try
        {
            await _productService.UpdateAsync(dto);
            return Ok(new { message = "Məhsul məlumatları yeniləndi!" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid companyId)
    {
        try
        {
            await _productService.DeleteAsync(id, companyId);
            return Ok(new { message = "Məhsul silindi!" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// İki parametrdə də günün əvvəli (00:00)dırsa — köhnə davranış: inclusive təqvim günləri.
    /// </summary>
    private static (DateTime Start, DateTime End) NormalizeDeletedReportRange(DateTime start, DateTime end)
    {
        var startMid = start.TimeOfDay == TimeSpan.Zero;
        var endMid = end.TimeOfDay == TimeSpan.Zero;
        if (startMid && endMid)
            return (start.Date, end.Date.AddDays(1).AddTicks(-1));
        return (start, end);
    }

    [HttpGet("deleted-report")]
    public async Task<IActionResult> GetDeletedReport(
        [FromQuery] DateTime start,
        [FromQuery] DateTime end,
        [FromQuery] Guid companyId)
    {
        if (companyId == Guid.Empty) return BadRequest("Şirkət ID-si mütləqdir!");
        var (s, e) = NormalizeDeletedReportRange(start, end);
        var result = await _productService.GetDeletedReportAsync(s, e, companyId);
        result.OrderLineDeletions = await _auditLogService.GetProductDeletionLogsInRangeAsync(s, e, companyId);
        result.TotalCount = result.Items.Count + result.OrderLineDeletions.Count;
        return Ok(result);
    }

    [HttpGet("stock-status")] // Yeni ünvan: api/Products/stock-status
    public async Task<IActionResult> GetStockStatus(
    [FromQuery] Guid companyId,
    [FromQuery] int skip = 0,
    [FromQuery] int take = 10,
    [FromQuery] string? search = null)
    {
        var (items, totalCount) = await _productService.GetStockStatusAsync(companyId, skip, take, search);
        return Ok(new { items, totalCount });
    }
}