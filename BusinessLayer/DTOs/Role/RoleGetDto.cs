namespace BusinessLayer.DTOs.Role;

public class RoleGetDto
{
    public Guid Id { get; set; }
    public string NameAz { get; set; }
    public string NameRu { get; set; }
    public string NameEn { get; set; }
    public bool IsAdmin { get; set; }
    public Guid CompanyId { get; set; }
    public List<int> Permissions { get; set; }
}