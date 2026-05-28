using System.Collections.Generic;
using BusinessLayer.DTOs.BossTelegram;

namespace BusinessLayer.Services.Abstractions;

public interface IBossTelegramChatService
{
    Task<List<BossTelegramChatRowDto>> ListAsync(Guid companyId, CancellationToken ct = default);
    Task LinkAsync(Guid companyId, Guid userId, long chatId, CancellationToken ct = default);
    Task UnlinkAsync(Guid companyId, long chatId, CancellationToken ct = default);
    /// <summary>Terminaldakı cari chat siyahısı ilə DB-ni tam uyğunlaşdırır (boş siyahı = hamısını sil).</summary>
    Task SyncSubscriberChatIdsAsync(Guid companyId, Guid userId, IReadOnlyList<long> chatIds, CancellationToken ct = default);
}
