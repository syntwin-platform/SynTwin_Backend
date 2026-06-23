namespace Syntwin.Application.Commands.Interfaces;

public interface IRobotBusyLock
{
    Task<bool> TryAcquireAsync(
        Guid robotId,
        Guid commandId,
        TimeSpan ttl,
        CancellationToken cancellationToken = default);

    Task ReleaseAsync(
        Guid robotId,
        Guid commandId,
        CancellationToken cancellationToken = default);
}