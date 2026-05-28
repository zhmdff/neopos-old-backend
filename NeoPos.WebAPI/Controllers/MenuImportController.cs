using BusinessLayer.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace NeoPos.WebAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class MenuImportController : ControllerBase
{
    private readonly IMenuImportService _menuImportService;

    public MenuImportController(IMenuImportService menuImportService)
    {
        _menuImportService = menuImportService;
    }

    /// <summary>Nümunə .xlsx — birinci vərəq Kateqoriyalar, ikinci Məhsullar.</summary>
    [HttpGet("template")]
    public IActionResult DownloadTemplate()
    {
        var bytes = _menuImportService.GetTemplateWorkbook();
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "neopos-menu-import-sablon.xlsx");
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] Guid companyId, CancellationToken cancellationToken)
    {
        try
        {
            var bytes = await _menuImportService.ExportMenuWorkbookAsync(companyId, cancellationToken);
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "neopos-menu-export.xlsx");
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("preview")]
    [RequestSizeLimit(25_000_000)]
    public async Task<IActionResult> Preview([FromQuery] Guid companyId, IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Fayl seçilməyib." });

        await using var stream = file.OpenReadStream();
        var result = await _menuImportService.PreviewAsync(stream, companyId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("apply")]
    [RequestSizeLimit(25_000_000)]
    public async Task<IActionResult> Apply([FromQuery] Guid companyId, IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Fayl seçilməyib." });

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _menuImportService.ApplyAsync(stream, companyId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
