using BusinessLayer.DTOs.Audit;

namespace BusinessLayer.DTOs.System;

public class BossNotifyRelayRequestDto
{
    public string TenantKey { get; set; } = "";

    /// <summary>audit | pendingDeletePush | pendingDeleteRefresh</summary>
    public string Kind { get; set; } = "audit";

    public AuditLogPostDto? Audit { get; set; }

    public BossPendingDeletePushRelayDto? PendingDeletePush { get; set; }
}

public class BossPendingDeletePushRelayDto
{
    public string PendingId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string RelativeUrl { get; set; } = "/boss/dashboard";
}
