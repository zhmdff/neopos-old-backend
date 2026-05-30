using BusinessLayer.DTOs.Audit;

namespace BusinessLayer.Services.Abstractions;

/// <summary>SignalR, Web Push və Telegram vasitəsilə Boss tətbiqinə canlı bildiriş.</summary>
public interface IBossLiveNotifyDispatcher
{
    Task DispatchAuditAsync(Guid companyId, AuditLogPostDto dto, CancellationToken ct = default);

    Task DispatchPendingDeleteRefreshAsync(Guid companyId, CancellationToken ct = default);

    Task DispatchPendingDeletePushAsync(
        Guid companyId,
        string pendingId,
        string title,
        string body,
        CancellationToken ct = default);
}
