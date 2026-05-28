namespace BusinessLayer.DTOs.Role;

public class RolePostDto
{
    public string NameAz { get; set; }
    public bool IsAdmin { get; set; }
    public Guid CompanyId { get; set; }
    public List<int> Permissions { get; set; }
}   