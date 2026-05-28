namespace BusinessLayer.DTOs.User;

public class UserGetDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; }
    public string Username { get; set; }
    public string PinCode { get; set; }
    public bool IsActive { get; set; }
    public Guid RoleId { get; set; }

    public string RoleNameAz { get; set; }
    public bool RoleIsAdmin { get; set; }

    public List<int> Permissions { get; set; }

    /// <summary>Yalnız öz hesabını GET edən istifadəçi üçün doldurulur (JWT id == tələb olunan id).</summary>
    public string? PanelPassword { get; set; }
}