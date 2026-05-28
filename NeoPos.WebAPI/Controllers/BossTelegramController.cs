using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NeoPos.WebAPI.Controllers;

/// <summary>
/// Terminal (Electron) Telegram — bot token yalnız <c>appsettings.json</c> «BossTelegram:BotToken»-dən.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BossTelegramController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public BossTelegramController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Electron lokal bot üçün token (UI-da soruşulmur). Yalnız konfiqurasiya faylından.
    /// </summary>
    [HttpGet("terminal-bot-config")]
    public ActionResult<BossTelegramTerminalBotConfigDto> GetTerminalBotConfig()
    {
        var token = _configuration["BossTelegram:BotToken"]?.Trim();
        var hasToken = !string.IsNullOrEmpty(token);
        return Ok(new BossTelegramTerminalBotConfigDto
        {
            HasToken = hasToken,
            BotToken = hasToken ? token : null,
            Source = "appsettings",
        });
    }

    public sealed class BossTelegramTerminalBotConfigDto
    {
        public bool HasToken { get; set; }
        public string? BotToken { get; set; }
        public string Source { get; set; } = "appsettings";
    }
}
