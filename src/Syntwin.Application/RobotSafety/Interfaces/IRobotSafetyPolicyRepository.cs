using Syntwin.Domain.Entities;

namespace Syntwin.Application.RobotSafety.Interfaces;

public interface IRobotSafetyPolicyRepository
{
    Task<RobotSafetyPolicy?> GetActiveRobotPolicyAsync(
        Guid robotId,
        CancellationToken cancellationToken = default);

    Task<RobotSafetyPolicy?> GetActiveCompanyPolicyAsync(
        Guid companyId,
        CancellationToken cancellationToken = default);

    Task<RobotSafetyPolicy?> GetCompanyPolicyForUpdateAsync(
        Guid companyId,
        CancellationToken cancellationToken = default);

    Task<RobotSafetyPolicy?> GetRobotPolicyForUpdateAsync(
        Guid robotId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        RobotSafetyPolicy policy,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}