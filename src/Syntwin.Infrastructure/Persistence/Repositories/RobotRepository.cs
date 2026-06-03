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

    public async Task<IReadOnlyList<Robot>> ListByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Robots
            .Where(robot =>
                robot.UserId == userId &&
                robot.Status != RobotStatus.Disabled)
            .OrderByDescending(robot => robot.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<Robot?> GetByIdAsync(Guid robotId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Robots
            .FirstOrDefaultAsync(robot => robot.Id == robotId, cancellationToken);
    }

    public Task<int> CountActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Robots
            .CountAsync(
                robot =>
                    robot.UserId == userId &&
                    robot.Status != RobotStatus.Disabled,
                cancellationToken);
    }

    public async Task AddAsync(Robot robot, CancellationToken cancellationToken = default)
    {
        await _dbContext.Robots.AddAsync(robot, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
