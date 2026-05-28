using Microsoft.AspNetCore.Http;

namespace DAL.Server.Service;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? CompanyId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirst("CompanyId")?.Value;

            if (Guid.TryParse(claim, out Guid companyId))
            {
                return companyId;
            }
            return null;
        }
    }
}
