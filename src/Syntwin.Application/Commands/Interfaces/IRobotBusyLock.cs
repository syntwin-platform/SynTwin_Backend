namespace Syntwin.Application.Commands.Interfaces;

public interface IRobotBusyLock
{
    Task<Guid?> GetOwnerAsync(
        Guid robotId,
        CancellationToken cancellationToken = default);

    Task<bool> TryAcquireAsync(
        Guid robotId,
        Guid commandId,
        TimeSpan ttl,
        CancellationToken cancellationToken = default);

    Task ReleaseAsync(
        Guid robotId,
        Guid commandId,
        CancellationToken cancellationToken = default);

    Task<bool> TryTransferAsync(
        Guid robotId,
        Guid expectedOwnerId,
        Guid newOwnerId,
        TimeSpan ttl,
        CancellationToken cancellationToken = default);

    Task<bool> RenewAsync(
        Guid robotId,
        Guid ownerId,
        TimeSpan ttl,
        CancellationToken cancellationToken = default);
}
