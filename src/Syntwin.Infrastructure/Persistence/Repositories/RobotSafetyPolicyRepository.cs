using Microsoft.EntityFrameworkCore;
using Syntwin.Application.RobotSafety.Interfaces;
using Syntwin.Domain.Entities;
using Syntwin.Domain.Enums;

namespace Syntwin.Infrastructure.Persistence;

public sealed class RobotSafetyPolicyRepository : IRobotSafetyPolicyRepository
{
    private readonly SyntwinDbContext _dbContext;

    public RobotSafetyPolicyRepository(SyntwinDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<RobotSafetyPolicy?> GetActiveRobotPolicyAsync(
        Guid robotId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.RobotSafetyPolicies
            .AsNoTracking()
            .Where(policy =>
                policy.RobotId == robotId &&
                policy.Scope == RobotSafetyPolicyScope.Robot &&
                policy.IsActive)
            .OrderByDescending(policy => policy.UpdatedAt ?? policy.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<RobotSafetyPolicy?> GetActiveCompanyPolicyAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.RobotSafetyPolicies
            .AsNoTracking()
            .Where(policy =>
                policy.CompanyId == companyId &&
                policy.RobotId == null &&
                policy.Scope == RobotSafetyPolicyScope.Company &&
                policy.IsActive)
            .OrderByDescending(policy => policy.UpdatedAt ?? policy.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<RobotSafetyPolicy?> GetCompanyPolicyForUpdateAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.RobotSafetyPolicies
            .FirstOrDefaultAsync(
                policy =>
                    policy.CompanyId == companyId &&
                    policy.RobotId == null &&
                    policy.Scope == RobotSafetyPolicyScope.Company &&
                    policy.IsActive,
                cancellationToken);
    }

    public Task<RobotSafetyPolicy?> GetRobotPolicyForUpdateAsync(
        Guid robotId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.RobotSafetyPolicies
            .FirstOrDefaultAsync(
                policy =>
                    policy.RobotId == robotId &&
                    policy.Scope == RobotSafetyPolicyScope.Robot &&
                    policy.IsActive,
                cancellationToken);
    }

    public async Task AddAsync(
        RobotSafetyPolicy policy,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.RobotSafetyPolicies.AddAsync(policy, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}