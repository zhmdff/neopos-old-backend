using BusinessLayer.DTOs.Purchase;
using BusinessLayer.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace NeoPos.WebAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PurchasesController : ControllerBase
{
    private readonly IPurchaseService _purchaseService;

    public PurchasesController(IPurchaseService purchaseService)
    {
        _purchaseService = purchaseService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(PurchasePostDto dto)
    {
        try
        {
            await _purchaseService.CreateAsync(dto);
            return Ok(new { message = "Mədaxil uğurla tamamlandı!" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("company/{companyId}")]
    public async Task<IActionResult> GetAll(Guid companyId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        // 🔥 Əgər ID boşdursa, birbaşa BadRequest qaytarırıq ki, 400 xətası anlaşılsın
        if (companyId == Guid.Empty) return BadRequest(new { message = "CompanyId tələb olunur!" });

        try
        {
            var (items, totalCount) = await _purchaseService.GetAllByCompanyIdAsync(companyId, pageNumber, pageSize);
            return Ok(new { items, totalCount });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var result = await _purchaseService.GetByIdAsync(id);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}