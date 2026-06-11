using Syntwin.Domain.Enums;

namespace Syntwin.Application.Robots.Interfaces;

public interface IRobotAccessService
{
    Task<IReadOnlyDictionary<Guid, CompanyMemberRole>> ListCompanyRolesAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<CompanyMemberRole?> GetCompanyRoleAsync(
        Guid userId,
        Guid companyId,
        CancellationToken cancellationToken = default);
}
