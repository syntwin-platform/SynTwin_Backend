using Syntwin.Application.Companies.Interfaces;
using Syntwin.Application.Robots.Interfaces;
using Syntwin.Domain.Enums;

namespace Syntwin.Application.Robots.Services;

public sealed class RobotAccessService : IRobotAccessService
{
    private readonly ICompanyRepository _companyRepository;

    public RobotAccessService(ICompanyRepository companyRepository)
    {
        _companyRepository = companyRepository;
    }

    public async Task<IReadOnlyDictionary<Guid, CompanyMemberRole>> ListCompanyRolesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var memberships = await _companyRepository.ListMembershipsAsync(userId, cancellationToken);

        return memberships
            .Where(member =>
                member.Company?.Status == CompanyStatus.Active &&
                member.User?.Status == UserStatus.Active)
            .ToDictionary(member => member.CompanyId, member => member.Role);
    }

    public async Task<CompanyMemberRole?> GetCompanyRoleAsync(
        Guid userId,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var membership = await _companyRepository.GetMembershipAsync(
            companyId,
            userId,
            cancellationToken);

        return membership?.Company?.Status == CompanyStatus.Active &&
               membership.User?.Status == UserStatus.Active
            ? membership.Role
            : null;
    }
}
