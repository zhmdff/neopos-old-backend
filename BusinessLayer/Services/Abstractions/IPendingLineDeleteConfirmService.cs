using BusinessLayer.DTOs.PendingLineDelete;

namespace BusinessLayer.Services.Abstractions;

public interface IPendingLineDeleteConfirmService
{
    Task RegisterAsync(Guid companyId, PendingLineDeleteRegisterDto dto, CancellationToken ct = default);

    Task<List<PendingLineDeleteActiveItemDto>> GetActiveAsync(Guid companyId, CancellationToken ct = default);

    /// <summary>pending | accepted | rejected | expired | not_found</summary>
    Task<(string status, bool? accepted)> GetStatusAsync(Guid companyId, string pendingId, CancellationToken ct = default);

    /// <summary>İlk uğurlu cavab qalibdir. Artıq bağlanıbsa cari nəticə qaytarılır.</summary>
    Task<(string status, bool? accepted)> TryResolveAsync(Guid companyId, string pendingId, bool accepted, CancellationToken ct = default);

    /// <summary>
    /// Yalnız <paramref name="pendingId"/> ilə (Telegram callback) — şirkət DB-dən tapılır.
    /// </summary>
    Task<(string status, bool? accepted)> TryResolveByPendingIdAsync(string pendingId, bool accepted, CancellationToken ct = default);
}
