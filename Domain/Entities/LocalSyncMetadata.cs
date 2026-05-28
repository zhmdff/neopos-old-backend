using Domain.Common;

namespace Domain.Entities;

public class LocalSyncMetadata : BaseEntity
{
    public DateTime? LastSuccessfulSyncAt { get; set; }
    public string? LastSyncStatus { get; set; }
    public string? TenantKey { get; set; }
}
