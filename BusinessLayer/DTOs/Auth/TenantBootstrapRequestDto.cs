namespace BusinessLayer.DTOs.Auth;

/// <summary>
/// Gizli bootstrap: yeni şirkət, admin rolu və ilk istifadəçi (yalnız serverdə təyin olunmuş açar ilə).
/// </summary>
public class TenantBootstrapRequestDto
{
    public string SetupSecret { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;

    /// <summary>Boşdursa "Administrator"</summary>
    public string? AdminRoleName { get; set; }

    public string AdminFullName { get; set; } = string.Empty;
    public string AdminUsername { get; set; } = string.Empty;
    public string AdminPassword { get; set; } = string.Empty;

    /// <summary>Terminal PIN (məs. 4 rəqəm). Boş ola bilər — sonra təyin edilir.</summary>
    public string? AdminPinCode { get; set; }
}
