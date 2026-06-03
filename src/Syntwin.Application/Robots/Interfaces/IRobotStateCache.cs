using Syntwin.Application.Robots.Dtos;

namespace Syntwin.Application.Robots.Interfaces;

public interface IRobotStateCache
{
    Task SetOnlineAsync(
        Guid robotId,
        DateTimeOffset lastSeenAt,
        CancellationToken cancellationToken = default);

    Task<bool> IsOnlineAsync(
        Guid robotId,
        CancellationToken cancellationToken = default);

    Task<DateTimeOffset?> GetLastSeenAsync(
        Guid robotId,
        CancellationToken cancellationToken = default);

    Task SetLatestAsync(
        RobotLatestStateResponse state,
        CancellationToken cancellationToken = default);

    Task<RobotLatestStateResponse?> GetLatestAsync(
        Guid robotId,
        CancellationToken cancellationToken = default);
}
