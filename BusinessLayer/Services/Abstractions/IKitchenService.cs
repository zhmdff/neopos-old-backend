using BusinessLayer.DTOs.Kitchen;

namespace BusinessLayer.Services.Abstractions;

public interface IKitchenService
{
    /// <param name="flushPending">
    /// true: «Mətbəxə göndər» — mətbəxə hələ ötürülməmiş miqdar da çap olunur.
    /// false: silmə/ləğv sonrası — yalnız əvvəl mətbəxə gedən sətirlər üzrə azalma/qeyd/ləğv; gözləyən miqdar çap olunmur.
    /// </param>
    Task<List<KitchenPrintGroupDto>> ProcessKitchenDeltaAsync(
        Guid orderHeaderId,
        Guid companyId,
        bool broadcastPrintToTerminals = false,
        bool flushPending = true);
}
