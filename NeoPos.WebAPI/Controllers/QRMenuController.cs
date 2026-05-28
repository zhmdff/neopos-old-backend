using BusinessLayer.DTOs.QRMenu;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NeoPos.WebAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class QRMenuController : ControllerBase
{
    private readonly IQRMenuService _qrMenuService;
    private readonly IWebHostEnvironment _env;

    public QRMenuController(IQRMenuService qrMenuService, IWebHostEnvironment env)
    {
        _qrMenuService = qrMenuService;
        _env = env;
    }

    [HttpGet("full-menu/{slug}")]
    public async Task<IActionResult> GetFullMenu(string slug)
    {
        var result = await _qrMenuService.GetFullMenuBySlugAsync(slug);
        if (result == null) return NotFound(new { message = "Restoran tapılmadı." });
        return Ok(result);
    }

    [HttpGet("settings/{companyId}")]
    public async Task<IActionResult> GetSettings(Guid companyId)
    {
        var settings = await _qrMenuService.GetSettingsByCompanyIdAsync(companyId);
        return Ok(settings);
    }

    [HttpPost("settings/{companyId}")]
    public async Task<IActionResult> UpdateSettings(Guid companyId, [FromBody] QRMenuSettingDto dto)
    {
        var result = await _qrMenuService.UpdateSettingsAsync(companyId, dto);
        return result ? Ok(new { message = "Uğurlu!" }) : BadRequest();
    }

    [HttpPost("upload-gallery-image")]
    public async Task<IActionResult> UploadGalleryImage(IFormFile file)
    {
        if (file == null || file.Length == 0) return BadRequest("Fayl yoxdur.");

        string rootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        string folderPath = Path.Combine(rootPath, "uploads", "gallery");
        if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

        string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
        string fullPath = Path.Combine(folderPath, fileName);

        using (var fileStream = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(fileStream);
        }

        return Ok(new { imageUrl = $"/uploads/gallery/{fileName}" });
    }

    [HttpPost("update-category-orders")]
    public async Task<IActionResult> UpdateCategoryOrders([FromBody] List<OrderUpdateDto> dtos)
    {
        var result = await _qrMenuService.UpdateCategoryOrdersAsync(dtos);
        return result ? Ok() : BadRequest();
    }

    [HttpPost("update-product-orders")]
    public async Task<IActionResult> UpdateProductOrders([FromBody] List<OrderUpdateDto> dtos)
    {
        var result = await _qrMenuService.UpdateProductOrdersAsync(dtos);
        return result ? Ok() : BadRequest();
    }


}