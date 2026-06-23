using Syntwin.Domain.Entities;

namespace Syntwin.Application.Robots.Interfaces;

public interface IRobotRuntimeSessionRepository
{
    Task<RobotRuntimeSession?> GetOpenByRobotIdAsync(
        Guid robotId,
        CancellationToken cancellationToken = default);

    Task<RobotRuntimeSession?> GetByIdAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        RobotRuntimeSession session,
        CancellationToken cancellationToken = default);

    Task<bool> TryAddOpenSessionAsync(
        RobotRuntimeSession session,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
