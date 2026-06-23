using Microsoft.Extensions.Options;
using Syntwin.Application.Realtime.Dtos;
using Syntwin.Application.Realtime.Interfaces;
using Syntwin.Application.Robots.Interfaces;
using Syntwin.Application.Robots.Options;
using Syntwin.Domain.Entities;
using Syntwin.Domain.Enums;
using Syntwin.Application.Common.Interfaces;
namespace Syntwin.Api.BackgroundServices;

public sealed class RobotOfflineMonitorService : BackgroundService
{
    private const string WorkerLockKey = "locks:workers:robot-offline-monitor";
    private readonly IDistributedLock _distributedLock;
    private readonly TimeSpan _lockTtl;
    private const string HeartbeatTimeoutEndReason = "HeartbeatTimeout";
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RobotOfflineMonitorService> _logger;
    private readonly TimeSpan _monitorInterval;

    public RobotOfflineMonitorService(
     IServiceScopeFactory scopeFactory,
     IOptions<RobotRuntimeOptions> options,
     ILogger<RobotOfflineMonitorService> logger,
     IDistributedLock distributedLock)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _distributedLock = distributedLock;

        var intervalSeconds = Math.Max(1, options.Value.OfflineMonitorIntervalSeconds);
        _monitorInterval = TimeSpan.FromSeconds(intervalSeconds);

        var lockTtlSeconds = Math.Max(intervalSeconds * 2, options.Value.OfflineMonitorLockTtlSeconds);
        _lockTtl = TimeSpan.FromSeconds(lockTtlSeconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var workerLock = await _distributedLock.TryAcquireAsync(
     WorkerLockKey,
     _lockTtl,
     stoppingToken);

                if (workerLock is not null)
                {
                    await MarkExpiredOnlineRobotsOfflineAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Failed to monitor offline robot status.");
            }

            try
            {
                await Task.Delay(_monitorInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private async Task MarkExpiredOnlineRobotsOfflineAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var robotRepository = scope.ServiceProvider.GetRequiredService<IRobotRepository>();
        var runtimeSessionRepository = scope.ServiceProvider.GetRequiredService<IRobotRuntimeSessionRepository>();
        var robotStateCache = scope.ServiceProvider.GetRequiredService<IRobotStateCache>();
        var realtimeNotifier = scope.ServiceProvider.GetRequiredService<IRobotRealtimeNotifier>();

        var now = DateTimeOffset.UtcNow;
        var dueRobotIds = await robotStateCache.ListPresenceDueRobotIdsAsync(
            now,
            take: 100,
            cancellationToken);

        if (dueRobotIds.Count == 0)
        {
            return;
        }

        var statusEvents = new List<RobotStatusChangedEvent>();
        var offlineRobotIds = new List<Guid>();

        foreach (var robotId in dueRobotIds)
        {
            var isOnline = await robotStateCache.IsOnlineAsync(robotId, cancellationToken);

            if (isOnline)
            {
                continue;
            }

            var robot = await robotRepository.GetByIdAsync(robotId, cancellationToken);

            if (robot is null ||
                robot.Status == RobotStatus.Disabled ||
                robot.Status != RobotStatus.Online)
            {
                await robotStateCache.ClearCurrentRuntimeSessionAsync(robotId, cancellationToken);
                await robotStateCache.RemovePresenceDueAsync(robotId, cancellationToken);
                continue;
            }

            var redisLastSeenAt = await robotStateCache.GetLastSeenAsync(
                robot.Id,
                cancellationToken);

            var endedAt = redisLastSeenAt ?? robot.LastSeenAt ?? now;

            var runtimeSession = await GetOpenRuntimeSessionAsync(
                runtimeSessionRepository,
                robotStateCache,
                robot.Id,
                cancellationToken);

            if (runtimeSession is not null)
            {
                runtimeSession.LastSeenAt = endedAt;
                runtimeSession.EndedAt = endedAt;
                runtimeSession.DetectedOfflineAt = now;
                runtimeSession.DurationSeconds = Math.Max(
                    0,
                    (long)(endedAt - runtimeSession.StartedAt).TotalSeconds);
                runtimeSession.EndReason = HeartbeatTimeoutEndReason;
                runtimeSession.UpdatedAt = now;
            }

            robot.Status = RobotStatus.Offline;
            robot.LastSeenAt = endedAt;
            robot.UpdatedAt = now;

            offlineRobotIds.Add(robot.Id);
            statusEvents.Add(new RobotStatusChangedEvent
            {
                RobotId = robot.Id,
                Status = RobotStatus.Offline.ToString(),
                IsOnline = false,
                ChangedAt = now,
                LastSeenAt = robot.LastSeenAt
            });
        }

        if (statusEvents.Count == 0)
        {
            return;
        }

        await runtimeSessionRepository.SaveChangesAsync(cancellationToken);
        foreach (var robotId in offlineRobotIds)
        {
            await robotStateCache.ClearCurrentRuntimeSessionAsync(robotId, cancellationToken);
            await robotStateCache.RemovePresenceDueAsync(robotId, cancellationToken);
        }

        foreach (var statusEvent in statusEvents)
        {
            await realtimeNotifier.NotifyRobotStatusChangedAsync(
                statusEvent,
                cancellationToken);
        }
    }

    private static async Task<RobotRuntimeSession?> GetOpenRuntimeSessionAsync(
    IRobotRuntimeSessionRepository runtimeSessionRepository,
    IRobotStateCache robotStateCache,
    Guid robotId,
    CancellationToken cancellationToken)
    {
        var cachedSessionId = await robotStateCache.GetCurrentRuntimeSessionIdAsync(
            robotId,
            cancellationToken);

        if (cachedSessionId.HasValue)
        {
            var cachedSession = await runtimeSessionRepository.GetByIdAsync(
                cachedSessionId.Value,
                cancellationToken);

            if (cachedSession?.EndedAt is null)
            {
                return cachedSession;
            }
        }

        return await runtimeSessionRepository.GetOpenByRobotIdAsync(
            robotId,
            cancellationToken);
    }
}