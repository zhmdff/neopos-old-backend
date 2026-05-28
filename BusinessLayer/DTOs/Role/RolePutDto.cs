namespace BusinessLayer.DTOs.Role;

public class RolePutDto
{
    public Guid Id { get; set; }
    public string NameAz { get; set; }
    public List<int> Permissions { get; set; }
    public Guid CompanyId { get; set; }
}