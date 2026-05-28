using BusinessLayer.DTOs.Auth;

namespace BusinessLayer.Services.Abstractions;

public interface ITenantBootstrapService
{
    Task<LoginResponseDTO> BootstrapAsync(TenantBootstrapRequestDto request);
}
