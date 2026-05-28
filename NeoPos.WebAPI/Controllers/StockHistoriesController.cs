using BusinessLayer.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace NeoPos.WebAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class StockHistoriesController : ControllerBase
{
    private readonly IStockHistoryService _historyService;

    public StockHistoriesController(IStockHistoryService historyService)
    {
        _historyService = historyService;
    }

    [HttpGet("company/{companyId}")]
    public async Task<IActionResult> GetHistory(Guid companyId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        // Standart yoxlamalar
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;

        var (items, totalCount) = await _historyService.GetAllByCompanyIdAsync(companyId, pageNumber, pageSize);

        return Ok(new
        {
            items,
            totalCount,
            pageNumber,
            pageSize
        });
    }
}