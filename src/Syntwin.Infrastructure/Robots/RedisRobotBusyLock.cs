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

    private readonly IDatabase _database;

    public RedisRobotBusyLock(IConnectionMultiplexer connectionMultiplexer)
    {
        _database = connectionMultiplexer.GetDatabase();
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

    private static string BusyLockKey(Guid robotId)
    {
        return $"robot:{robotId}:busy-lock";
    }
}