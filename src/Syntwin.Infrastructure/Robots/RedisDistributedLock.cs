using StackExchange.Redis;
using Syntwin.Application.Common.Interfaces;

namespace Syntwin.Infrastructure.Robots;

public sealed class RedisDistributedLock : IDistributedLock
{
    private const string ReleaseScript = """
if redis.call('GET', KEYS[1]) == ARGV[1] then
    return redis.call('DEL', KEYS[1])
end
return 0
""";

    private readonly IDatabase _database;

    public RedisDistributedLock(IConnectionMultiplexer connectionMultiplexer)
    {
        _database = connectionMultiplexer.GetDatabase();
    }

    public async Task<IDistributedLockHandle?> TryAcquireAsync(
        string key,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        var lockValue = Guid.NewGuid().ToString("N");

        var acquired = await _database.StringSetAsync(
            key,
            lockValue,
            ttl,
            When.NotExists);

        return acquired
            ? new RedisDistributedLockHandle(_database, key, lockValue)
            : null;
    }

    private sealed class RedisDistributedLockHandle : IDistributedLockHandle
    {
        private readonly IDatabase _database;
        private readonly string _lockValue;
        private bool _disposed;

        public RedisDistributedLockHandle(
            IDatabase database,
            string key,
            string lockValue)
        {
            _database = database;
            Key = key;
            _lockValue = lockValue;
        }

        public string Key { get; }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            await _database.ScriptEvaluateAsync(
                ReleaseScript,
                new RedisKey[] { Key },
                new RedisValue[] { _lockValue });
        }
    }
}