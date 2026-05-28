namespace Domain.Common.Entities;

public class User : AuditableCompanyEntity
{
    public string FullName { get; set; }
    public string Username { get; set; }
    public string PasswordHash { get; set; }
    public string? PinCode { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid RoleId { get; set; }
    public Role Role { get; set; }

    /// <summary>
    /// Bir nəfərin bir neçə şirkətdə (restoranda) ayrı admin hesablarını Boss-da bir hesabda görməsi üçün.
    /// Eyni LinkedAccountId olan user-lar Boss selector-da birlikdə görünəcək.
    /// </summary>
    public Guid? LinkedAccountId { get; set; }
}
