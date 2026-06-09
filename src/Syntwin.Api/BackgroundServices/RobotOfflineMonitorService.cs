using Microsoft.Extensions.Options;
using Syntwin.Application.Realtime.Dtos;
using Syntwin.Application.Realtime.Interfaces;
using Syntwin.Application.Robots.Interfaces;
using Syntwin.Domain.Enums;
using Syntwin.Infrastructure.Robots;

namespace Syntwin.Api.BackgroundServices;

public sealed class RobotOfflineMonitorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RobotOfflineMonitorService> _logger;
    private readonly TimeSpan _monitorInterval;

    public RobotOfflineMonitorService(
        IServiceScopeFactory scopeFactory,
        IOptions<RobotRuntimeOptions> options,
        ILogger<RobotOfflineMonitorService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        var intervalSeconds = Math.Max(1, options.Value.OfflineMonitorIntervalSeconds);
        _monitorInterval = TimeSpan.FromSeconds(intervalSeconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await MarkExpiredOnlineRobotsOfflineAsync(stoppingToken);
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

            await Task.Delay(_monitorInterval, stoppingToken);
        }
    }

    private async Task MarkExpiredOnlineRobotsOfflineAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var robotRepository = scope.ServiceProvider.GetRequiredService<IRobotRepository>();
        var robotStateCache = scope.ServiceProvider.GetRequiredService<IRobotStateCache>();
        var realtimeNotifier = scope.ServiceProvider.GetRequiredService<IRobotRealtimeNotifier>();

        var onlineRobots = await robotRepository.ListByStatusAsync(
            RobotStatus.Online,
            cancellationToken);

        if (onlineRobots.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var statusEvents = new List<RobotStatusChangedEvent>();

        foreach (var robot in onlineRobots)
        {
            var isOnline = await robotStateCache.IsOnlineAsync(robot.Id, cancellationToken);

            if (isOnline)
            {
                continue;
            }

            var redisLastSeenAt = await robotStateCache.GetLastSeenAsync(
                robot.Id,
                cancellationToken);

            robot.Status = RobotStatus.Offline;
            robot.LastSeenAt = redisLastSeenAt ?? robot.LastSeenAt;
            robot.UpdatedAt = now;

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

        await robotRepository.SaveChangesAsync(cancellationToken);

        foreach (var statusEvent in statusEvents)
        {
            await realtimeNotifier.NotifyRobotStatusChangedAsync(
                statusEvent,
                cancellationToken);
        }
    }
}