using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Syntwin.Application.Devices.Dtos;
using Syntwin.Application.Robots.Dtos;
using Syntwin.Application.Robots.Interfaces;
using Syntwin.Application.Robots.Options;
using System.Text.Json;
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
        var onlineTtl = TimeSpan.FromSeconds(Math.Max(1, _options.OnlineTtlSeconds));
        var lastSeenTtl = TimeSpan.FromSeconds(Math.Max(onlineTtl.TotalSeconds, _options.LastSeenTtlSeconds));
        var presenceDueAt = lastSeenAt.Add(onlineTtl).ToUnixTimeMilliseconds();

        await _database.StringSetAsync(OnlineKey(robotId), "1", onlineTtl);
        await _database.StringSetAsync(LastSeenKey(robotId), lastSeenAt.ToString("O"), lastSeenTtl);
        await _database.SortedSetAddAsync(PresenceDueKey(), robotId.ToString(), presenceDueAt);
        await _database.SetAddAsync(LastSeenDirtyKey(), robotId.ToString());

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

    public async Task<IReadOnlyDictionary<Guid, RobotRuntimeStateCacheEntry>> GetRuntimeStatesAsync(
    IReadOnlyCollection<Guid> robotIds,
    CancellationToken cancellationToken = default)
    {
        if (robotIds.Count == 0)
        {
            return new Dictionary<Guid, RobotRuntimeStateCacheEntry>();
        }

        var ids = robotIds.Distinct().ToArray();
        var onlineKeys = ids
            .Select(robotId => (RedisKey)OnlineKey(robotId))
            .ToArray();

        var lastSeenKeys = ids
            .Select(robotId => (RedisKey)LastSeenKey(robotId))
            .ToArray();

        var onlineValuesTask = _database.StringGetAsync(onlineKeys);
        var lastSeenValuesTask = _database.StringGetAsync(lastSeenKeys);

        await Task.WhenAll(onlineValuesTask, lastSeenValuesTask);

        var onlineValues = await onlineValuesTask;
        var lastSeenValues = await lastSeenValuesTask;

        var result = new Dictionary<Guid, RobotRuntimeStateCacheEntry>(ids.Length);

        for (var index = 0; index < ids.Length; index++)
        {
            DateTimeOffset? lastSeenAt = null;

            if (lastSeenValues[index].HasValue &&
                DateTimeOffset.TryParse(lastSeenValues[index].ToString(), out var parsedLastSeenAt))
            {
                lastSeenAt = parsedLastSeenAt;
            }

            result[ids[index]] = new RobotRuntimeStateCacheEntry(
                onlineValues[index].HasValue,
                lastSeenAt);
        }

        return result;
    }
    public async Task<IReadOnlyList<Guid>> ListPresenceDueRobotIdsAsync(
    DateTimeOffset dueAt,
    int take,
    CancellationToken cancellationToken = default)
    {
        var values = await _database.SortedSetRangeByScoreAsync(
            PresenceDueKey(),
            double.NegativeInfinity,
            dueAt.ToUnixTimeMilliseconds(),
            Exclude.None,
            Order.Ascending,
            skip: 0,
            take: Math.Max(1, take));

        return values
            .Select(value => Guid.TryParse(value.ToString(), out var robotId)
                ? robotId
                : (Guid?)null)
            .Where(robotId => robotId.HasValue)
            .Select(robotId => robotId!.Value)
            .ToList();
    }

    public async Task<IReadOnlyList<Guid>> ListLastSeenDirtyRobotIdsAsync(
    int take,
    CancellationToken cancellationToken = default)
    {
        var values = await _database.SetMembersAsync(LastSeenDirtyKey());

        return values
            .Take(Math.Max(1, take))
            .Select(value => Guid.TryParse(value.ToString(), out var robotId)
                ? robotId
                : (Guid?)null)
            .Where(robotId => robotId.HasValue)
            .Select(robotId => robotId!.Value)
            .ToList();
    }

    public async Task RemoveLastSeenDirtyAsync(
        Guid robotId,
        CancellationToken cancellationToken = default)
    {
        await _database.SetRemoveAsync(LastSeenDirtyKey(), robotId.ToString());
    }
    public async Task RemovePresenceDueAsync(
        Guid robotId,
        CancellationToken cancellationToken = default)
    {
        await _database.SortedSetRemoveAsync(PresenceDueKey(), robotId.ToString());
    }

    public async Task SetCurrentRuntimeSessionAsync(
    Guid robotId,
    Guid sessionId,
    TimeSpan ttl,
    CancellationToken cancellationToken = default)
    {
        await _database.StringSetAsync(
            RuntimeSessionKey(robotId),
            sessionId.ToString(),
            ttl);
    }

    public async Task<Guid?> GetCurrentRuntimeSessionIdAsync(
        Guid robotId,
        CancellationToken cancellationToken = default)
    {
        var value = await _database.StringGetAsync(RuntimeSessionKey(robotId));

        if (!value.HasValue)
        {
            return null;
        }

        return Guid.TryParse(value.ToString(), out var sessionId)
            ? sessionId
            : null;
    }

    public async Task ClearCurrentRuntimeSessionAsync(
        Guid robotId,
        CancellationToken cancellationToken = default)
    {
        await _database.KeyDeleteAsync(RuntimeSessionKey(robotId));
    }

    public async Task SetDeviceSessionAsync(
    DeviceRuntimeSession session,
    TimeSpan ttl,
    CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(session, JsonOptions);

        await _database.StringSetAsync(
            DeviceSessionKey(session.RobotId),
            json,
            ttl);
    }

    public async Task<DeviceRuntimeSession?> GetDeviceSessionAsync(
        Guid robotId,
        CancellationToken cancellationToken = default)
    {
        var value = await _database.StringGetAsync(DeviceSessionKey(robotId));

        if (!value.HasValue)
        {
            return null;
        }

        return JsonSerializer.Deserialize<DeviceRuntimeSession>(
            value.ToString(),
            JsonOptions);
    }

    public async Task ClearDeviceSessionAsync(
        Guid robotId,
        CancellationToken cancellationToken = default)
    {
        await _database.KeyDeleteAsync(DeviceSessionKey(robotId));
    }

    public async Task SetLatestAsync(
        RobotLatestStateResponse state,
        CancellationToken cancellationToken = default)
    {
        var ttl = TimeSpan.FromSeconds(_options.LatestStateTtlSeconds);
        var json = JsonSerializer.Serialize(state, JsonOptions);

        await _database.StringSetAsync(LatestKey(state.RobotId), json, ttl);
    }

    public async Task<bool> ShouldBroadcastTelemetryAsync(
    Guid robotId,
    TimeSpan minInterval,
    CancellationToken cancellationToken = default)
    {
        if (minInterval <= TimeSpan.Zero)
        {
            return true;
        }

        return await _database.StringSetAsync(
            LatestBroadcastThrottleKey(robotId),
            DateTimeOffset.UtcNow.ToString("O"),
            minInterval,
            When.NotExists);
    }

    public async Task AddTelemetryViewerAsync(
    Guid robotId,
    string connectionId,
    TimeSpan ttl,
    CancellationToken cancellationToken = default)
    {
        await _database.SetAddAsync(
            TelemetryViewersKey(robotId),
            connectionId);

        await _database.KeyExpireAsync(
            TelemetryViewersKey(robotId),
            ttl);

        await _database.SetAddAsync(
            TelemetryViewerConnectionKey(connectionId),
            robotId.ToString());

        await _database.KeyExpireAsync(
            TelemetryViewerConnectionKey(connectionId),
            ttl);
    }

    public async Task RemoveTelemetryViewerAsync(
        Guid robotId,
        string connectionId,
        CancellationToken cancellationToken = default)
    {
        await _database.SetRemoveAsync(
            TelemetryViewersKey(robotId),
            connectionId);

        await _database.SetRemoveAsync(
            TelemetryViewerConnectionKey(connectionId),
            robotId.ToString());
    }

    public async Task<IReadOnlyList<Guid>> ListTelemetryViewerRobotIdsAsync(
        string connectionId,
        CancellationToken cancellationToken = default)
    {
        var values = await _database.SetMembersAsync(
            TelemetryViewerConnectionKey(connectionId));

        return values
            .Select(value => Guid.TryParse(value.ToString(), out var robotId)
                ? robotId
                : (Guid?)null)
            .Where(robotId => robotId.HasValue)
            .Select(robotId => robotId!.Value)
            .ToList();
    }

    public async Task ClearTelemetryViewerConnectionAsync(
        string connectionId,
        CancellationToken cancellationToken = default)
    {
        var robotIds = await ListTelemetryViewerRobotIdsAsync(
            connectionId,
            cancellationToken);

        foreach (var robotId in robotIds)
        {
            await _database.SetRemoveAsync(
                TelemetryViewersKey(robotId),
                connectionId);
        }

        await _database.KeyDeleteAsync(
            TelemetryViewerConnectionKey(connectionId));
    }

    public async Task<bool> HasTelemetryViewersAsync(
        Guid robotId,
        CancellationToken cancellationToken = default)
    {
        return await _database.SetLengthAsync(
            TelemetryViewersKey(robotId)) > 0;
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

    private static string RuntimeSessionKey(Guid robotId)
    {
        return $"robot:{robotId}:runtime-session";
    }

    private static string DeviceSessionKey(Guid robotId)
    {
        return $"robot:{robotId}:session";
    }

    private static string LatestKey(Guid robotId)
    {
        return $"robot:{robotId}:latest";
    }

    private static string LatestBroadcastThrottleKey(Guid robotId)
    {
        return $"robot:{robotId}:last-telemetry-broadcast-at";
    }

    private static string TelemetryViewersKey(Guid robotId)
    {
        return $"robot:{robotId}:telemetry:viewers";
    }

    private static string TelemetryViewerConnectionKey(string connectionId)
    {
        return $"telemetry-viewer-connection:{connectionId}:robots";
    }
    private static string LastSeenDirtyKey()
    {
        return "robots:lastseen:dirty";
    }

    private static string PresenceDueKey()
    {
        return "robots:presence:due";
    }
}