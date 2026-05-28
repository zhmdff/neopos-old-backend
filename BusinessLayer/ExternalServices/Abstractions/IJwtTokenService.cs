using Domain.Common.Entities;

namespace BusinessLayer.ExternalServices.Abstractions;

public interface IJwtTokenService
{
    Task<string> GenerateJwtToken(User user);

    Task<string> GenerateWaiterSessionToken(Guid companyId, Guid cashShiftId, string? companyName);
}