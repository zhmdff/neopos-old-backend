namespace Domain.Common.Entities;

public class Role : AuditableCompanyEntity
{
    public string NameAz { get; set; }
    public string NameRu { get; set; }
    public string NameEn { get; set; }
    public bool IsAdmin { get; set; } = false;
    public int[]? Permissions { get; set; }
    public ICollection<User> Users { get; set; } = [];
}