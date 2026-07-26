using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Syntwin.Application.FactoryRuns.Interfaces;
using Syntwin.Application.FactoryRuns.Models;
using Syntwin.Application.Robots.Options;
using System.Globalization;
using System.Text.Json;

namespace Syntwin.Infrastructure.FactoryRuns;

public sealed class RedisFactoryRunArmBarrier : IFactoryRunArmBarrier
{
    private const string RegisterScript = """
        local armedKey = KEYS[1]
        local metadataKey = KEYS[2]
        local sealKey = KEYS[3]
        local readyKey = KEYS[4]

        local targetId = ARGV[1]
        local expected = tonumber(ARGV[2])
        local acceptedAtUnixMs = ARGV[3]
        local ttlMs = tonumber(ARGV[4])
        local sealLeaseMs = tonumber(ARGV[5])

        redis.call('SADD', armedKey, targetId)
        redis.call('HSETNX', metadataKey, 'firstArmUnixMs', acceptedAtUnixMs)
        redis.call(
            'HSET',
            metadataKey,
            'lastArmUnixMs', acceptedAtUnixMs,
            'expectedParticipantCount', expected)
        redis.call('PEXPIRE', armedKey, ttlMs)
        redis.call('PEXPIRE', metadataKey, ttlMs)

        local armedCount = redis.call('SCARD', armedKey)
        local readyExists = redis.call('EXISTS', readyKey)
        local shouldSeal = 0

        if readyExists == 0 and armedCount >= expected then
            local acquired = redis.call(
                'SET',
                sealKey,
                targetId,
                'NX',
                'PX',
                sealLeaseMs)
            if acquired then
                shouldSeal = 1
            end
        end

        return {armedCount, expected, shouldSeal, readyExists}
        """;

    private const string PublishReadyScript = """
        local sealKey = KEYS[1]
        local readyKey = KEYS[2]
        local sealOwner = ARGV[1]
        local scheduledStartAtUtc = ARGV[2]
        local expectedParticipantCount = ARGV[3]
        local stepDurationsJson = ARGV[4]
        local ttlMs = tonumber(ARGV[5])

        local currentOwner = redis.call('GET', sealKey)
        if currentOwner ~= sealOwner then
            return 0
        end

        redis.call(
            'HSET',
            readyKey,
            'scheduledStartAtUtc', scheduledStartAtUtc,
            'expectedParticipantCount', expectedParticipantCount,
            'stepDurationsJson', stepDurationsJson)
        redis.call('PEXPIRE', readyKey, ttlMs)
        redis.call('DEL', sealKey)
        return 1
        """;

    private const string RestoreReadyScript = """
        local sealKey = KEYS[1]
        local readyKey = KEYS[2]
        local scheduledStartAtUtc = ARGV[1]
        local expectedParticipantCount = ARGV[2]
        local stepDurationsJson = ARGV[3]
        local ttlMs = tonumber(ARGV[4])

        redis.call(
            'HSET',
            readyKey,
            'scheduledStartAtUtc', scheduledStartAtUtc,
            'expectedParticipantCount', expectedParticipantCount,
            'stepDurationsJson', stepDurationsJson)
        redis.call('PEXPIRE', readyKey, ttlMs)
        redis.call('DEL', sealKey)
        return 1
        """;

    private const string ReleaseSealScript = """
        local currentOwner = redis.call('GET', KEYS[1])
        if currentOwner == ARGV[1] then
            return redis.call('DEL', KEYS[1])
        end
        return 0
        """;

    private readonly IDatabase _database;
    private readonly TimeSpan _barrierTtl;
    private readonly TimeSpan _sealLease;

    public RedisFactoryRunArmBarrier(
        IConnectionMultiplexer connectionMultiplexer,
        IOptions<RobotRuntimeOptions> options)
    {
        _database = connectionMultiplexer.GetDatabase();
        _barrierTtl = TimeSpan.FromSeconds(Math.Clamp(
            options.Value.FactoryRunBarrierTtlSeconds,
            60,
            3600));
        _sealLease = TimeSpan.FromSeconds(Math.Clamp(
            options.Value.FactoryRunBarrierSealLeaseSeconds,
            5,
            120));
    }

    public async Task<FactoryRunArmBarrierRegistration> RegisterAsync(
        Guid factoryRunId,
        Guid targetId,
        int expectedParticipantCount,
        DateTimeOffset acceptedAtUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var prefix = GetKeyPrefix(factoryRunId);
        var result = (RedisResult[]?)await _database.ScriptEvaluateAsync(
            RegisterScript,
            [
                $"{prefix}:armed",
                $"{prefix}:metadata",
                $"{prefix}:seal",
                $"{prefix}:ready"
            ],
            [
                targetId.ToString("N"),
                Math.Max(1, expectedParticipantCount),
                acceptedAtUtc.ToUnixTimeMilliseconds(),
                (long)_barrierTtl.TotalMilliseconds,
                (long)_sealLease.TotalMilliseconds
            ]);

        var armedCount = result is { Length: > 0 } ? (int)result[0] : 0;
        var expected = result is { Length: > 1 }
            ? (int)result[1]
            : Math.Max(1, expectedParticipantCount);
        var shouldSeal = result is { Length: > 2 } && (int)result[2] == 1;
        var ready = result is { Length: > 3 } && (int)result[3] == 1
            ? await GetReadyAsync(factoryRunId, cancellationToken)
            : null;

        return new FactoryRunArmBarrierRegistration(
            armedCount,
            expected,
            shouldSeal,
            ready);
    }

    public async Task<FactoryRunArmBarrierReadyState?> GetReadyAsync(
        Guid factoryRunId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var values = await _database.HashGetAsync(
            $"{GetKeyPrefix(factoryRunId)}:ready",
            [
                "scheduledStartAtUtc",
                "expectedParticipantCount",
                "stepDurationsJson"
            ]);

        if (values.Length != 3 ||
            values[0].IsNullOrEmpty ||
            values[1].IsNullOrEmpty)
        {
            return null;
        }

        if (!DateTimeOffset.TryParse(
                values[0].ToString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var scheduledStartAtUtc) ||
            !int.TryParse(
                values[1].ToString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var expectedParticipantCount))
        {
            return null;
        }

        var stepDurations = values[2].IsNullOrEmpty
            ? []
            : JsonSerializer.Deserialize<int[]>(values[2].ToString()) ?? [];

        return new FactoryRunArmBarrierReadyState(
            scheduledStartAtUtc,
            expectedParticipantCount,
            stepDurations);
    }

    public async Task PublishReadyAsync(
        Guid factoryRunId,
        Guid sealOwnerTargetId,
        FactoryRunArmBarrierReadyState ready,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var prefix = GetKeyPrefix(factoryRunId);
        var result = (int)await _database.ScriptEvaluateAsync(
            PublishReadyScript,
            [
                $"{prefix}:seal",
                $"{prefix}:ready"
            ],
            [
                sealOwnerTargetId.ToString("N"),
                ready.ScheduledStartAtUtc.ToString("O", CultureInfo.InvariantCulture),
                ready.ExpectedParticipantCount,
                JsonSerializer.Serialize(ready.StepDurationsMs),
                (long)_barrierTtl.TotalMilliseconds
            ]);

        if (result != 1)
        {
            throw new InvalidOperationException(
                "Factory run barrier seal ownership was lost before publish.");
        }
    }

    public async Task RestoreReadyAsync(
        Guid factoryRunId,
        FactoryRunArmBarrierReadyState ready,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var prefix = GetKeyPrefix(factoryRunId);
        await _database.ScriptEvaluateAsync(
            RestoreReadyScript,
            [
                $"{prefix}:seal",
                $"{prefix}:ready"
            ],
            [
                ready.ScheduledStartAtUtc.ToString("O", CultureInfo.InvariantCulture),
                ready.ExpectedParticipantCount,
                JsonSerializer.Serialize(ready.StepDurationsMs),
                (long)_barrierTtl.TotalMilliseconds
            ]);
    }

    public async Task ReleaseSealAsync(
        Guid factoryRunId,
        Guid sealOwnerTargetId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await _database.ScriptEvaluateAsync(
            ReleaseSealScript,
            [$"{GetKeyPrefix(factoryRunId)}:seal"],
            [sealOwnerTargetId.ToString("N")]);
    }

    private static string GetKeyPrefix(Guid factoryRunId)
    {
        return $"factory-run:{factoryRunId:N}:arm-v2";
    }
}
