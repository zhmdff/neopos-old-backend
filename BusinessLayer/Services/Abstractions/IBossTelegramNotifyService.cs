namespace BusinessLayer.Services.Abstractions;

public interface IBossTelegramNotifyService
{
    /// <summary>Audit hadisəsi üçün Telegram bildirişi (şirkət prefs + BossTelegramChats).</summary>
    Task TryNotifyAuditAsync(
        Guid companyId,
        string action,
        string? description,
        string? userName,
        string? tableName,
        string? hallName,
        string timeHHmm,
        DateTime whenLocal,
        CancellationToken ct = default);
}
