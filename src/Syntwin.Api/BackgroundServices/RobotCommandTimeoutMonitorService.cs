using Microsoft.Extensions.Options;
using Syntwin.Application.AuditLogs.Interfaces;
using Syntwin.Application.Commands.Interfaces;
using Syntwin.Application.Realtime.Dtos;
using Syntwin.Application.Realtime.Interfaces;
using Syntwin.Domain.Entities;
using Syntwin.Domain.Enums;
using Syntwin.Infrastructure.Robots;

namespace Syntwin.Api.BackgroundServices;

public sealed class RobotCommandTimeoutMonitorService : BackgroundService
{
    private const string TimeoutMessage = "Command timed out before device submitted a result.";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RobotCommandTimeoutMonitorService> _logger;
    private readonly TimeSpan _monitorInterval;

    public RobotCommandTimeoutMonitorService(
        IServiceScopeFactory scopeFactory,
        IOptions<RobotRuntimeOptions> options,
        ILogger<RobotCommandTimeoutMonitorService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        var intervalSeconds = Math.Max(1, options.Value.CommandTimeoutMonitorIntervalSeconds);
        _monitorInterval = TimeSpan.FromSeconds(intervalSeconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await MarkExpiredCommandsFailedAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to monitor command timeouts.");
            }

            await Task.Delay(_monitorInterval, stoppingToken);
        }
    }

    private async Task MarkExpiredCommandsFailedAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var commandRepository = scope.ServiceProvider.GetRequiredService<IRobotCommandRepository>();
        var auditLogRepository = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();
        var realtimeNotifier = scope.ServiceProvider.GetRequiredService<IRobotRealtimeNotifier>();

        var now = DateTimeOffset.UtcNow;
        var expiredCommands = await commandRepository.ListExpiredActiveCommandsAsync(
            now,
            cancellationToken);

        if (expiredCommands.Count == 0)
        {
            return;
        }

        var completedEvents = new List<CommandCompletedEvent>();

        foreach (var command in expiredCommands)
        {
            var existingResult = await commandRepository.GetResultByCommandIdAsync(
                command.Id,
                cancellationToken);

            if (existingResult is not null)
            {
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
        }

        if (completedEvents.Count == 0)
        {
            return;
        }

        await commandRepository.SaveChangesAsync(cancellationToken);

        foreach (var completedEvent in completedEvents)
        {
            await realtimeNotifier.NotifyCommandCompletedAsync(
                completedEvent,
                cancellationToken);
        }
    }
}