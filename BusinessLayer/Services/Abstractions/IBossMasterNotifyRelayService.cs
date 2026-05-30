using BusinessLayer.DTOs.Audit;
using BusinessLayer.DTOs.System;

namespace BusinessLayer.Services.Abstractions;

/// <summary>Tenant rejimində Boss bildirişlərini bulud master API-yə ötürür.</summary>
public interface IBossMasterNotifyRelayService
{
    Task TryRelayAuditAsync(AuditLogPostDto dto, CancellationToken ct = default);

    Task TryRelayPendingDeleteAsync(
        Guid companyId,
        string pendingId,
        string title,
        string body,
        CancellationToken ct = default);
}
