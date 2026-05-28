namespace DAL.Server.Service;

public interface ICurrentUserService
{
    Guid? CompanyId { get; }
}
