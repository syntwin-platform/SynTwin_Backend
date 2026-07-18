using Microsoft.EntityFrameworkCore;
using Syntwin.Application.Robots.Interfaces;
using Syntwin.Domain.Entities;
using Syntwin.Domain.Enums;

namespace Syntwin.Infrastructure.Persistence;

public sealed class RobotRepository : IRobotRepository
{
    private readonly SyntwinDbContext _dbContext;

    public RobotRepository(SyntwinDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Robot>> ListAccessibleByUserIdAsync(
        Guid userId,
        Guid? companyId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Robots
            .AsNoTracking()
            .Include(robot => robot.SceneBinding)
            .Where(robot => _dbContext.CompanyMembers.Any(member =>
            member.CompanyId == robot.CompanyId &&
            member.UserId == userId &&
            member.IsActive &&
            member.Company != null &&
            member.Company.Status == CompanyStatus.Active) &&
        robot.Status != RobotStatus.Disabled)
    .AsQueryable();

        if (companyId.HasValue)
        {
            query = query.Where(robot => robot.CompanyId == companyId.Value);
        }

        return await query
            .OrderByDescending(robot => robot.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<Robot?> GetByIdAsync(Guid robotId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Robots
            .Include(robot => robot.SceneBinding)
            .FirstOrDefaultAsync(robot => robot.Id == robotId, cancellationToken);
    }

    public Task<Robot?> GetByIdReadOnlyAsync(
    Guid robotId,
    CancellationToken cancellationToken = default)
    {
        return _dbContext.Robots
            .AsNoTracking()
            .Include(robot => robot.SceneBinding)
            .FirstOrDefaultAsync(robot => robot.Id == robotId, cancellationToken);
    }

    public Task<RobotSceneBinding?> GetSceneBindingByRobotIdReadOnlyAsync(
        Guid robotId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.RobotSceneBindings
            .AsNoTracking()
            .FirstOrDefaultAsync(binding => binding.RobotId == robotId, cancellationToken);
    }

    public Task<int> CountActiveOwnedByUserIdAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Robots
    .AsNoTracking()
    .CountAsync(
        robot =>
            _dbContext.CompanyMembers.Any(member =>
                member.CompanyId == robot.CompanyId &&
                member.UserId == ownerUserId &&
                member.IsActive &&
                member.Role == CompanyMemberRole.Owner &&
                member.Company != null &&
                member.Company.Status == CompanyStatus.Active) &&
            robot.Status != RobotStatus.Disabled,
        cancellationToken);
    }

    public async Task AddAsync(Robot robot, CancellationToken cancellationToken = default)
    {
        await _dbContext.Robots.AddAsync(robot, cancellationToken);
    }

    public async Task AddSceneBindingAsync(
    RobotSceneBinding sceneBinding,
    CancellationToken cancellationToken = default)
    {
        await _dbContext.RobotSceneBindings.AddAsync(sceneBinding, cancellationToken);
    }

    public async Task<bool> UpdateSceneBindingByRobotIdAsync(
        RobotSceneBinding sceneBinding,
        CancellationToken cancellationToken = default)
    {
        var affectedRows = await _dbContext.RobotSceneBindings
            .Where(binding => binding.RobotId == sceneBinding.RobotId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(binding => binding.SceneType, sceneBinding.SceneType)
                .SetProperty(binding => binding.BaseX, sceneBinding.BaseX)
                .SetProperty(binding => binding.BaseY, sceneBinding.BaseY)
                .SetProperty(binding => binding.BaseZ, sceneBinding.BaseZ)
                .SetProperty(binding => binding.BaseYaw, sceneBinding.BaseYaw)
                .SetProperty(binding => binding.UrdfPath, sceneBinding.UrdfPath)
                .SetProperty(binding => binding.PrimPath, sceneBinding.PrimPath)
                .SetProperty(binding => binding.RosNamespace, sceneBinding.RosNamespace)
                .SetProperty(binding => binding.GraphPath, sceneBinding.GraphPath)
                .SetProperty(binding => binding.UpdatedAt, sceneBinding.UpdatedAt),
                cancellationToken);

        return affectedRows > 0;
    }

    public async Task<IReadOnlyList<Robot>> ListByStatusAsync(
    RobotStatus status,
    CancellationToken cancellationToken = default)
    {
        return await _dbContext.Robots
    .AsNoTracking()
    .Where(robot => robot.Status == status)
    .OrderBy(robot => robot.LastSeenAt)
    .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Robot>> ListByIdsAsync(
    IReadOnlyCollection<Guid> robotIds,
    CancellationToken cancellationToken = default)
    {
        if (robotIds.Count == 0)
        {
            return Array.Empty<Robot>();
        }

        return await _dbContext.Robots
            .Include(robot => robot.SceneBinding)
            .Where(robot => robotIds.Contains(robot.Id))
            .ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
