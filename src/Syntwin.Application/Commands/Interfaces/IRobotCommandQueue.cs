using Syntwin.Domain.Entities;

namespace Syntwin.Application.Commands.Interfaces;

public interface IRobotCommandQueue
{
    Task EnqueueAsync(
        RobotCommand command,
        CancellationToken cancellationToken = default);

    Task RequeueAsync(
        RobotCommand command,
        CancellationToken cancellationToken = default);

    Task<Guid?> DequeueAsync(
        Guid robotId,
        bool safetyOnly = false,
        CancellationToken cancellationToken = default);

    Task<Guid?> DequeueOrWaitAsync(
        Guid robotId,
        bool safetyOnly,
        TimeSpan waitTimeout,
        CancellationToken cancellationToken = default);
}