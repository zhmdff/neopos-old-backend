namespace BusinessLayer.DTOs.User;

public class UserPostDto
{
    public string FullName { get; set; } 
    public string Username { get; set; } 
    public string Password { get; set; }
    public string? PinCode { get; set; }
    public Guid RoleId { get; set; }
    public Guid CompanyId { get; set; }
    public bool IsActive { get; set; } = true;
}