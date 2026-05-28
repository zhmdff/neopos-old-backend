using Microsoft.AspNetCore.SignalR;

namespace BusinessLayer.Hubs; // 🔥 Namespace artıq budur

public class NotificationHub : Hub
{
    public async Task JoinCompanyGroup(string companyId)
    {
        var key = (companyId ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(key)) return;
        await Groups.AddToGroupAsync(Context.ConnectionId, key);
    }
}