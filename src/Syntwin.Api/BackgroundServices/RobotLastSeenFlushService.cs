using Microsoft.Extensions.Options;
using Syntwin.Application.Robots.Interfaces;
using Syntwin.Application.Robots.Options;
using Syntwin.Application.Common.Interfaces;

namespace Syntwin.Api.BackgroundServices;

public sealed class RobotLastSeenFlushService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RobotLastSeenFlushService> _logger;
    private readonly TimeSpan _flushInterval;
    private readonly int _batchSize;
    private const string WorkerLockKey = "locks:workers:robot-lastseen-flush";
    private readonly IDistributedLock _distributedLock;
    private readonly TimeSpan _lockTtl;

    public RobotLastSeenFlushService(
    IServiceScopeFactory scopeFactory,
    IOptions<RobotRuntimeOptions> options,
    ILogger<RobotLastSeenFlushService> logger,
    IDistributedLock distributedLock)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _distributedLock = distributedLock;

        var intervalSeconds = Math.Max(10, options.Value.LastSeenFlushIntervalSeconds);
        _flushInterval = TimeSpan.FromSeconds(intervalSeconds);
        _batchSize = Math.Max(1, options.Value.LastSeenFlushBatchSize);

        var lockTtlSeconds = Math.Max(intervalSeconds * 2, options.Value.LastSeenFlushLockTtlSeconds);
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
                    await FlushLastSeenAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to flush robot LastSeenAt values.");
            }

            try
            {
                await Task.Delay(_flushInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private async Task FlushLastSeenAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var robotStateCache = scope.ServiceProvider.GetRequiredService<IRobotStateCache>();
        var robotRepository = scope.ServiceProvider.GetRequiredService<IRobotRepository>();

        var dirtyRobotIds = await robotStateCache.ListLastSeenDirtyRobotIdsAsync(
            _batchSize,
            cancellationToken);

        if (dirtyRobotIds.Count == 0)
        {
            return;
        }

        var robots = await robotRepository.ListByIdsAsync(
            dirtyRobotIds,
            cancellationToken);

        var robotById = robots.ToDictionary(robot => robot.Id);
        var now = DateTimeOffset.UtcNow;
        var hasChanges = false;

        foreach (var robotId in dirtyRobotIds)
        {
            if (!robotById.TryGetValue(robotId, out var robot))
            {
                await robotStateCache.RemoveLastSeenDirtyAsync(robotId, cancellationToken);
                continue;
            }

            var lastSeenAt = await robotStateCache.GetLastSeenAsync(
                robotId,
                cancellationToken);

            if (!lastSeenAt.HasValue)
            {
                await robotStateCache.RemoveLastSeenDirtyAsync(robotId, cancellationToken);
                continue;
            }

            if (!robot.LastSeenAt.HasValue || robot.LastSeenAt < lastSeenAt)
            {
                robot.LastSeenAt = lastSeenAt;
                robot.UpdatedAt = now;
                hasChanges = true;
            }

            await robotStateCache.RemoveLastSeenDirtyAsync(robotId, cancellationToken);
        }

        if (hasChanges)
        {
            await robotRepository.SaveChangesAsync(cancellationToken);
        }
    }
}