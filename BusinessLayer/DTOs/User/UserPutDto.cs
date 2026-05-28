namespace BusinessLayer.DTOs.User;

public class UserPutDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } 
    public string Username { get; set; } 
    public string? PinCode { get; set; }
    public Guid RoleId { get; set; }
    public bool IsActive { get; set; }
    public Guid CompanyId { get; set; }

    /// <summary>Doludursa giriş parolu yenilənir (köhnə dəyər əvəzlənir).</summary>
    public string? Password { get; set; }
}