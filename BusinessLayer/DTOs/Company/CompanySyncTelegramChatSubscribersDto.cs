namespace BusinessLayer.DTOs.Company;

/// <summary>Electron-da /link ilə toplanan chat id-lərinin serverə köçürülməsi.</summary>
public class CompanySyncTelegramChatSubscribersDto
{
    public List<long> ChatIds { get; set; } = new();
}
