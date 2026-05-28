using BusinessLayer.DTOs.Audit;
using BusinessLayer.DTOs.Product;

namespace BusinessLayer.Services.Abstractions;

public interface IAuditLogService
{
    Task LogActionAsync(AuditLogPostDto dto);

    /// <param name="fromInclusive">UTC və ya Unspecified — DB ilə müqayisə üçün Bakı divar vaxtına çevrilir.</param>
    /// <param name="toInclusive">Daxil: bu vaxta qədər (o cümlədən).</param>
    Task<List<AuditLogGetDto>> GetCompanyLogsAsync(
        Guid companyId,
        int take = 50,
        DateTime? fromInclusive = null,
        DateTime? toInclusive = null);

    Task<List<AuditLogGetDto>> GetShiftLogsAsync(Guid shiftId, Guid companyId, int take = 50);

    /// <summary>
    /// Tarix aralığında «MƏHSUL SİLİNDİ» audit qeydləri (sifariş sətri ləğvi / silinmə).
    /// </summary>
    Task<List<OrderLineDeletionItemDto>> GetProductDeletionLogsInRangeAsync(DateTime start, DateTime end, Guid companyId);
}