using Microsoft.Extensions.Options;
using Syntwin.Application.AuditLogs.Interfaces;
using Syntwin.Application.Commands.Interfaces;
using Syntwin.Application.Realtime.Dtos;
using Syntwin.Application.Realtime.Interfaces;
using Syntwin.Domain.Entities;
using Syntwin.Domain.Enums;
using Syntwin.Application.Robots.Options;
using Syntwin.Application.Common.Interfaces;

namespace Syntwin.Api.BackgroundServices;

public sealed class RobotCommandTimeoutMonitorService : BackgroundService
{
    private const string TimeoutMessage = "Command timed out before device submitted a result.";
    private const string WorkerLockKey = "locks:workers:robot-command-timeout-monitor";
    private readonly IDistributedLock _distributedLock;
    private readonly TimeSpan _lockTtl;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RobotCommandTimeoutMonitorService> _logger;
    private readonly TimeSpan _monitorInterval;

    public RobotCommandTimeoutMonitorService(
     IServiceScopeFactory scopeFactory,
     IOptions<RobotRuntimeOptions> options,
     ILogger<RobotCommandTimeoutMonitorService> logger,
     IDistributedLock distributedLock)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _distributedLock = distributedLock;

        var intervalSeconds = Math.Max(1, options.Value.CommandTimeoutMonitorIntervalSeconds);
        _monitorInterval = TimeSpan.FromSeconds(intervalSeconds);

        var lockTtlSeconds = Math.Max(intervalSeconds * 2, options.Value.CommandTimeoutMonitorLockTtlSeconds);
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
                    await MarkExpiredCommandsFailedAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to monitor command timeouts.");
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

    private async Task MarkExpiredCommandsFailedAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var commandRepository = scope.ServiceProvider.GetRequiredService<IRobotCommandRepository>();
        var timeoutScheduler = scope.ServiceProvider.GetRequiredService<IRobotCommandTimeoutScheduler>();
        var auditLogRepository = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();
        var realtimeNotifier = scope.ServiceProvider.GetRequiredService<IRobotRealtimeNotifier>();
        var robotBusyLock = scope.ServiceProvider.GetRequiredService<IRobotBusyLock>();

        var now = DateTimeOffset.UtcNow;
        var dueCommandIds = await timeoutScheduler.ListDueCommandIdsAsync(
            now,
            take: 100,
            cancellationToken);

        if (dueCommandIds.Count == 0)
        {
            return;
        }
        var completedEvents = new List<CommandCompletedEvent>();
        var busyLockReleases = new List<(Guid RobotId, Guid CommandId)>();

        foreach (var commandId in dueCommandIds)
        {
            var command = await commandRepository.GetByIdAsync(
                commandId,
                cancellationToken);

            if (command is null)
            {
                await timeoutScheduler.RemoveAsync(commandId, cancellationToken);
                continue;
            }
            var existingResult = await commandRepository.GetResultByCommandIdAsync(
                command.Id,
                cancellationToken);

            if (existingResult is not null)
            {
                await timeoutScheduler.RemoveAsync(command.Id, cancellationToken);
                continue;
            }

            if (command.Status is not CommandStatus.Pending and not CommandStatus.Sent)
            {
                await timeoutScheduler.RemoveAsync(command.Id, cancellationToken);
                continue;
            }

            if (!command.TimeoutAt.HasValue)
            {
                await timeoutScheduler.RemoveAsync(command.Id, cancellationToken);
                continue;
            }

            if (command.TimeoutAt > now)
            {
                await timeoutScheduler.ScheduleAsync(command, cancellationToken);
                continue;
            }

            command.Status = CommandStatus.Failed;
            command.CompletedAt = now;
            command.FailureReason = "Timeout";

            var commandResult = new CommandResult
            {
                CommandId = command.Id,
                RobotId = command.RobotId,
                Success = false,
                Message = TimeoutMessage,
                RawPayloadJson = "{\"source\":\"RobotCommandTimeoutMonitorService\"}",
                CompletedAt = now
            };

            await commandRepository.AddCommandResultAsync(commandResult, cancellationToken);

            await auditLogRepository.AddAsync(new AuditLog
            {
                UserId = command.UserId,
                RobotId = command.RobotId,
                Action = "COMMAND_TIMEOUT",
                Message = TimeoutMessage,
                RawPayloadJson = command.PayloadJson,
                CreatedAt = now
            }, cancellationToken);

            completedEvents.Add(new CommandCompletedEvent
            {
                CommandId = command.Id,
                RobotId = command.RobotId,
                CommandType = command.CommandType.ToString(),
                CommandStatus = command.Status.ToString(),
                Success = false,
                Message = TimeoutMessage,
                CompletedAt = now
            });
            if (IsBusyLockCommand(command.CommandType))
            {
                busyLockReleases.Add((command.RobotId, command.Id));
            }
        }

        if (completedEvents.Count == 0)
        {
            return;
        }

        await commandRepository.SaveChangesAsync(cancellationToken);

        foreach (var completedEvent in completedEvents)
        {
            await timeoutScheduler.RemoveAsync(
                completedEvent.CommandId,
                cancellationToken);
        }

        foreach (var release in busyLockReleases)
        {
            await robotBusyLock.ReleaseAsync(
                release.RobotId,
                release.CommandId,
                cancellationToken);
        }

        foreach (var completedEvent in completedEvents)
        {
            await realtimeNotifier.NotifyCommandCompletedAsync(
                completedEvent,
                cancellationToken);
        }
    }

    private static bool IsBusyLockCommand(RobotCommandType commandType)
    {
        return commandType != RobotCommandType.EStop;
    }
}