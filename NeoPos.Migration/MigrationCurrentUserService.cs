using DAL.Server.Service;

namespace NeoPos.Migration;

/// <summary>Migration runs without HTTP context — no company filter on reads.</summary>
internal sealed class MigrationCurrentUserService : ICurrentUserService
{
    public Guid? CompanyId => null;
}
