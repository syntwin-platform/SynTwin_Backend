using System.Text.Json;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Syntwin.Application.Robots.Dtos;
using Syntwin.Application.Robots.Interfaces;

namespace Syntwin.Infrastructure.Robots;

public sealed class RedisRobotStateCache : IRobotStateCache
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IDatabase _database;
    private readonly RobotRuntimeOptions _options;

    public RedisRobotStateCache(
        IConnectionMultiplexer connectionMultiplexer,
        IOptions<RobotRuntimeOptions> options)
    {
        _database = connectionMultiplexer.GetDatabase();
        _options = options.Value;
    }

    public async Task SetOnlineAsync(
        Guid robotId,
        DateTimeOffset lastSeenAt,
        CancellationToken cancellationToken = default)
    {
        var ttl = TimeSpan.FromSeconds(_options.OnlineTtlSeconds);

        await _database.StringSetAsync(OnlineKey(robotId), "1", ttl);
        await _database.StringSetAsync(LastSeenKey(robotId), lastSeenAt.ToString("O"), ttl);
    }

    public async Task<bool> IsOnlineAsync(
        Guid robotId,
        CancellationToken cancellationToken = default)
    {
        return await _database.KeyExistsAsync(OnlineKey(robotId));
    }

    public async Task<DateTimeOffset?> GetLastSeenAsync(
        Guid robotId,
        CancellationToken cancellationToken = default)
    {
        var value = await _database.StringGetAsync(LastSeenKey(robotId));

        if (!value.HasValue)
        {
            return null;
        }

        return DateTimeOffset.TryParse(value.ToString(), out var lastSeenAt)
            ? lastSeenAt
            : null;
    }

    public async Task SetLatestAsync(
        RobotLatestStateResponse state,
        CancellationToken cancellationToken = default)
    {
        var ttl = TimeSpan.FromSeconds(_options.LatestStateTtlSeconds);
        var json = JsonSerializer.Serialize(state, JsonOptions);

        await _database.StringSetAsync(LatestKey(state.RobotId), json, ttl);
    }

    public async Task<RobotLatestStateResponse?> GetLatestAsync(
        Guid robotId,
        CancellationToken cancellationToken = default)
    {
        var value = await _database.StringGetAsync(LatestKey(robotId));

        if (!value.HasValue)
        {
            return null;
        }

        return JsonSerializer.Deserialize<RobotLatestStateResponse>(
            value.ToString(),
            JsonOptions);
    }

    private static string OnlineKey(Guid robotId)
    {
        return $"robot:{robotId}:online";
    }

    private static string LastSeenKey(Guid robotId)
    {
        return $"robot:{robotId}:last-seen";
    }

    private static string LatestKey(Guid robotId)
    {
        return $"robot:{robotId}:latest";
    }
}