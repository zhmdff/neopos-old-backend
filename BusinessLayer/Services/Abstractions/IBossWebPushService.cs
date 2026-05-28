namespace BusinessLayer.Services.Abstractions;

public interface IBossWebPushService
{
    /// <summary>VAPID public key (client subscribe üçün). Boşdursa Web Push söndürülüb.</summary>
    string? GetVapidPublicKey();

    Task UpsertSubscriptionAsync(Guid userId, Guid companyId, string endpoint, string p256dh, string auth, CancellationToken ct = default);

    Task RemoveByEndpointAsync(Guid userId, Guid companyId, string endpoint, CancellationToken ct = default);

    /// <summary>
    /// Seçilmiş kritik audit hadisələri üçün şirkətə abunə olan bütün Boss brauzer cihazlarına Web Push.
    /// </summary>
    /// <param name="relativeUrl">Bildirişə klikdə açılacaq səhifə (məs. /boss/audit-logs).</param>
    /// <param name="notificationTag">Eyni tag yeni bildirişi köhnəsini əvəz edir (OS-dan asılı).</param>
    Task NotifyCompanySubscribersAsync(
        Guid companyId,
        string title,
        string body,
        string relativeUrl = "/boss/audit-logs",
        string? notificationTag = null,
        CancellationToken ct = default);
}
