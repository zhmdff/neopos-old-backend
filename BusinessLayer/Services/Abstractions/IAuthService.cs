using BusinessLayer.DTOs.Auth;

namespace BusinessLayer.Services.Abstractions;

public interface IAuthService
{
    Task<LoginResponseDTO> LoginAsync(LoginRequestDTO request);

    Task<LoginResponseDTO> PinLoginAsync(PinLoginRequestDTO request);

    Task<LoginResponseDTO> WaiterShiftLoginAsync(WaiterShiftLoginRequestDTO request);

    Task<LoginResponseDTO> SwitchCompanyAsync(Guid currentUserId, Guid companyId);

    /// <summary>İki ayrı şirkətdə olan user hesablarını bir LinkedAccountId altında birləşdir.</summary>
    Task<LoginResponseDTO> LinkAccountsAsync(Guid currentUserId, Guid otherUserId);
}