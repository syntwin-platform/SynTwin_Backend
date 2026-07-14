using Syntwin.Domain.Entities;

namespace Syntwin.Application.Robots.Interfaces;

public interface IRobotModelRepository
{
    Task<IReadOnlyList<RobotModel>> ListActiveAsync(
        CancellationToken cancellationToken = default);

    Task<RobotModel?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}