namespace BusinessLayer.DTOs.Company;

/// <summary>Terminal (Electron) bildirişlərində bot token saxlananda serverə sinxron.</summary>
public class CompanyTelegramBotTokenPutDto
{
    /// <summary>Boş/null — serverdə token silinsin.</summary>
    public string? Token { get; set; }
}
