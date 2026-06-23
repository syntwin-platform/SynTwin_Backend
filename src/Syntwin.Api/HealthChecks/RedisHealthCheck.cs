using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Syntwin.Api.HealthChecks;

public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _redis;

    public RedisHealthCheck(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_redis.IsConnected)
        {
            return HealthCheckResult.Unhealthy("Redis is not connected.");
        }

        var database = _redis.GetDatabase();
        await database.PingAsync();

        return HealthCheckResult.Healthy("Redis is reachable.");
    }
}