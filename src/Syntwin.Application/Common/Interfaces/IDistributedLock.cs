namespace Syntwin.Application.Common.Interfaces;

public interface IDistributedLock
{
    Task<IDistributedLockHandle?> TryAcquireAsync(
        string key,
        TimeSpan ttl,
        CancellationToken cancellationToken = default);
}

public interface IDistributedLockHandle : IAsyncDisposable
{
    string Key { get; }
}