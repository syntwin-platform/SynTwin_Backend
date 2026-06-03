using Syntwin.Domain.Entities;

namespace Syntwin.Application.Robots.Interfaces;

public interface IRobotRepository
{
    Task<IReadOnlyList<Robot>> ListByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<Robot?> GetByIdAsync(Guid robotId, CancellationToken cancellationToken = default);

    Task<int> CountActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task AddAsync(Robot robot, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);


}