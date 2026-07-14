using StackExchange.Redis;
using Syntwin.Application.Commands.Interfaces;

namespace Syntwin.Infrastructure.Robots;

public sealed class RedisRobotBusyLock : IRobotBusyLock
{
    private const string ReleaseScript = """
if redis.call('GET', KEYS[1]) == ARGV[1] then
    return redis.call('DEL', KEYS[1])
end
return 0
""";

    private const string TransferScript = """
if redis.call('GET', KEYS[1]) == ARGV[1] then
    redis.call('SET', KEYS[1], ARGV[2], 'PX', ARGV[3])
    return 1
end
return 0
""";

    private const string RenewScript = """
if redis.call('GET', KEYS[1]) == ARGV[1] then
    return redis.call('PEXPIRE', KEYS[1], ARGV[2])
end
return 0
""";

    private readonly IDatabase _database;

    public RedisRobotBusyLock(IConnectionMultiplexer connectionMultiplexer)
    {
        _database = connectionMultiplexer.GetDatabase();
    }

    public async Task<Guid?> GetOwnerAsync(
        Guid robotId,
        CancellationToken cancellationToken = default)
    {
        var value = await _database.StringGetAsync(BusyLockKey(robotId));
        return value.HasValue && Guid.TryParse(value.ToString(), out var ownerId)
            ? ownerId
            : null;
    }

    public async Task<bool> TryAcquireAsync(
        Guid robotId,
        Guid commandId,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        return await _database.StringSetAsync(
            BusyLockKey(robotId),
            commandId.ToString(),
            ttl,
            When.NotExists);
    }

    public async Task ReleaseAsync(
        Guid robotId,
        Guid commandId,
        CancellationToken cancellationToken = default)
    {
        await _database.ScriptEvaluateAsync(
            ReleaseScript,
            new RedisKey[] { BusyLockKey(robotId) },
            new RedisValue[] { commandId.ToString() });
    }

    public async Task<bool> TryTransferAsync(
        Guid robotId,
        Guid expectedOwnerId,
        Guid newOwnerId,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        var result = await _database.ScriptEvaluateAsync(
            TransferScript,
            new RedisKey[] { BusyLockKey(robotId) },
            new RedisValue[]
            {
                expectedOwnerId.ToString(),
                newOwnerId.ToString(),
                ToTtlMilliseconds(ttl)
            });

        return (long)result == 1;
    }

    public async Task<bool> RenewAsync(
        Guid robotId,
        Guid ownerId,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        var result = await _database.ScriptEvaluateAsync(
            RenewScript,
            new RedisKey[] { BusyLockKey(robotId) },
            new RedisValue[] { ownerId.ToString(), ToTtlMilliseconds(ttl) });

        return (long)result == 1;
    }

    private static long ToTtlMilliseconds(TimeSpan ttl)
    {
        return Math.Max(1, (long)Math.Ceiling(ttl.TotalMilliseconds));
    }

    private static string BusyLockKey(Guid robotId)
    {
        return $"robot:{robotId}:busy-lock";
    }
}
