namespace Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; set; }
    public bool IsDeleted { get; set; } = false;
    public bool IsSynced { get; set; } = false;
}
