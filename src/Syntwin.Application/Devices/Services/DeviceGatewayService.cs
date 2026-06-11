using System.Text.Json;
using Syntwin.Application.AuditLogs.Interfaces;
using Syntwin.Application.Commands.Interfaces;
using Syntwin.Application.Common.Interfaces;
using Syntwin.Application.Devices.Dtos;
using Syntwin.Application.Devices.Interfaces;
using Syntwin.Application.Robots.Dtos;
using Syntwin.Application.Robots.Interfaces;
using Syntwin.Domain.Entities;
using Syntwin.Domain.Enums;
using Syntwin.Application.Realtime.Interfaces;
using Syntwin.Application.Realtime.Dtos;
namespace Syntwin.Application.Devices.Services;

public sealed class DeviceGatewayService : IDeviceGatewayService
{
    private static readonly TimeSpan SqlStatusUpdateInterval = TimeSpan.FromSeconds(10);

    private readonly IRobotRepository _robotRepository;
    private readonly IRobotCommandRepository _commandRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRobotStateCache _robotStateCache;
    private readonly IRobotRealtimeNotifier _realtimeNotifier;

    public DeviceGatewayService(
        IRobotRepository robotRepository,
        IRobotCommandRepository commandRepository,
        IAuditLogRepository auditLogRepository,
        IPasswordHasher passwordHasher,
        IRobotStateCache robotStateCache,
        IRobotRealtimeNotifier realtimeNotifier)
    {
        _robotRepository = robotRepository;
        _commandRepository = commandRepository;
        _auditLogRepository = auditLogRepository;
        _passwordHasher = passwordHasher;
        _robotStateCache = robotStateCache;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<bool?> HeartbeatAsync(
        Guid robotId,
        string deviceSecret,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        var auth = await AuthenticateAsync(
            robotId,
            deviceSecret,
            "heartbeat",
            ipAddress,
            cancellationToken);

        if (auth is null)
        {
            return null;
        }

        if (auth.Status == RobotStatus.Disabled)
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        var shouldBroadcastOnline = auth.Status != RobotStatus.Online;
        await _robotStateCache.SetOnlineAsync(robotId, now, cancellationToken);

        var shouldUpdateSql =
            auth.Status != RobotStatus.Online ||
            auth.LastSeenAt is null ||
            now - auth.LastSeenAt.Value >= SqlStatusUpdateInterval;
        if (shouldUpdateSql)
        {
            auth.Status = RobotStatus.Online;
            auth.LastSeenAt = now;
            auth.UpdatedAt = now;

            await _robotRepository.SaveChangesAsync(cancellationToken);

            if (shouldBroadcastOnline)
            {
                await _realtimeNotifier.NotifyRobotStatusChangedAsync(
                    CreateOnlineStatusChangedEvent(robotId, now),
                    cancellationToken);
            }
        }

        return true;
    }

    public async Task<bool?> SubmitTelemetryAsync(
        Guid robotId,
        string deviceSecret,
        DeviceTelemetryRequest request,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        var auth = await AuthenticateAsync(
            robotId,
            deviceSecret,
            "telemetry",
            ipAddress,
            cancellationToken);

        if (auth is null)
        {
            return null;
        }

        if (auth.Status == RobotStatus.Disabled)
        {
            return false;
        }

        if (request.RobotId != robotId)
        {
            throw new InvalidOperationException("RobotId in body does not match device header.");
        }

        if (request.JointAngles.Count != 6)
        {
            throw new InvalidOperationException("JointAngles must contain exactly 6 values.");
        }

        if (string.IsNullOrWhiteSpace(request.StatusCode))
        {
            throw new InvalidOperationException("StatusCode is required.");
        }

        var now = DateTimeOffset.UtcNow;
        var shouldBroadcastOnline = auth.Status != RobotStatus.Online;
        var timestamp = request.Timestamp ?? now;

        await _robotStateCache.SetOnlineAsync(robotId, now, cancellationToken);

        var latestState = new RobotLatestStateResponse
        {
            RobotId = robotId,
            IsOnline = true,
            Status = request.StatusCode.Trim(),
            TcpPose = request.TcpPose,
            JointAngles = request.JointAngles.ToArray(),
            Temperature = request.Temperature,
            CollisionWarning = request.CollisionWarning,
            LastSeenAt = now,
            Timestamp = timestamp,
            Source = "Redis"
        };

        await _robotStateCache.SetLatestAsync(latestState, cancellationToken);
        await _realtimeNotifier.NotifyTelemetryUpdatedAsync(latestState, cancellationToken);

        var shouldUpdateSql =
            auth.Status != RobotStatus.Online ||
            auth.LastSeenAt is null ||
            now - auth.LastSeenAt.Value >= SqlStatusUpdateInterval;

        if (shouldUpdateSql)
        {
            auth.Status = RobotStatus.Online;
            auth.LastSeenAt = now;
            auth.UpdatedAt = now;

            await _robotRepository.SaveChangesAsync(cancellationToken);

            if (shouldBroadcastOnline)
            {
                await _realtimeNotifier.NotifyRobotStatusChangedAsync(
                    CreateOnlineStatusChangedEvent(robotId, now),
                    cancellationToken);
            }
        }

        return true;
    }

    public async Task<DevicePendingCommandResult> TakePendingCommandAsync(
     Guid robotId,
     string deviceSecret,
     bool isBusy = false,
     string? ipAddress = null,
     CancellationToken cancellationToken = default)
    {
        var auth = await AuthenticateAsync(
            robotId,
            deviceSecret,
            "commands/pending",
            ipAddress,
            cancellationToken);

        if (auth is null)
        {
            return new DevicePendingCommandResult
            {
                IsAuthenticated = false
            };
        }

        if (auth.Status == RobotStatus.Disabled)
        {
            return new DevicePendingCommandResult
            {
                IsAuthenticated = true,
                IsDisabled = true
            };
        }

        var command = await _commandRepository.TakeNextPendingAsync(
            robotId,
            safetyOnly: isBusy,
            cancellationToken: cancellationToken);

        return new DevicePendingCommandResult
        {
            IsAuthenticated = true,
            Command = command is null ? null : ToPendingResponse(command)
        };
    }

    public async Task<DeviceCommandResultSubmitResult> SubmitCommandResultAsync(
        Guid robotId,
        string deviceSecret,
        DeviceCommandResultRequest request,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        var auth = await AuthenticateAsync(
            robotId,
            deviceSecret,
            "commands/result",
            ipAddress,
            cancellationToken);

        if (auth is null)
        {
            return new DeviceCommandResultSubmitResult
            {
                IsAuthenticated = false
            };
        }

        if (auth.Status == RobotStatus.Disabled)
        {
            return new DeviceCommandResultSubmitResult
            {
                IsAuthenticated = true,
                IsDisabled = true
            };
        }

        if (request.RobotId != robotId)
        {
            throw new InvalidOperationException("RobotId in body does not match device header.");
        }

        var requestedStatus = ValidateCommandResultStatus(request);

        var command = await _commandRepository.GetByIdForRobotAsync(
            request.CommandId,
            robotId,
            cancellationToken);

        if (command is null)
        {
            throw new InvalidOperationException("Command not found.");
        }

        var existingResult = await _commandRepository.GetResultByCommandIdAsync(
            request.CommandId,
            cancellationToken);

        if (existingResult is not null)
        {
            return new DeviceCommandResultSubmitResult
            {
                IsAuthenticated = true,
                Result = ToCommandResultResponse(command, existingResult, isDuplicate: true)
            };
        }

        var completedAt = request.CompletedAt ?? DateTimeOffset.UtcNow;

        command.Status = requestedStatus;
        command.CompletedAt = completedAt;
        command.FailureReason = requestedStatus == CommandStatus.Failed ? request.Message : null;

        var commandResult = new CommandResult
        {
            CommandId = command.Id,
            RobotId = robotId,
            Success = request.Success,
            Message = request.Message,
            RawPayloadJson = request.RawPayload?.GetRawText(),
            CompletedAt = completedAt
        };

        await _commandRepository.AddCommandResultAsync(commandResult, cancellationToken);

        await _auditLogRepository.AddAsync(new AuditLog
        {
            UserId = command.UserId,
            RobotId = robotId,
            Action = requestedStatus == CommandStatus.Completed
                ? "COMMAND_COMPLETED"
                : "COMMAND_FAILED",
            IpAddress = ipAddress,
            Message = request.Message,
            RawPayloadJson = request.RawPayload?.GetRawText(),
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);

        await _commandRepository.SaveChangesAsync(cancellationToken);

        await _realtimeNotifier.NotifyCommandCompletedAsync(
            ToCommandCompletedEvent(command, commandResult),
            cancellationToken);

        return new DeviceCommandResultSubmitResult
        {
            IsAuthenticated = true,
            Result = ToCommandResultResponse(command, commandResult, isDuplicate: false)
        };
    }

    private async Task<Robot?> AuthenticateAsync(
        Guid robotId,
        string deviceSecret,
        string endpointName,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var robot = await _robotRepository.GetByIdAsync(robotId, cancellationToken);

        if (string.IsNullOrWhiteSpace(deviceSecret))
        {
            await AddDeviceAuthFailedAuditAsync(
                robot,
                robotId,
                endpointName,
                ipAddress,
                cancellationToken);

            return null;
        }

        if (robot?.DeviceTokenHash is null)
        {
            await AddDeviceAuthFailedAuditAsync(
                robot,
                robotId,
                endpointName,
                ipAddress,
                cancellationToken);

            return null;
        }

        if (_passwordHasher.Verify(deviceSecret, robot.DeviceTokenHash))
        {
            return robot;
        }

        await AddDeviceAuthFailedAuditAsync(
            robot,
            robotId,
            endpointName,
            ipAddress,
            cancellationToken);

        return null;
    }

    private async Task AddDeviceAuthFailedAuditAsync(
        Robot? robot,
        Guid robotId,
        string endpointName,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        await _auditLogRepository.AddAsync(new AuditLog
        {
            RobotId = robot?.Id,
            Action = "DEVICE_AUTH_FAILED",
            IpAddress = ipAddress,
            Message = $"Device authentication failed on {endpointName}. RobotId header: {robotId}.",
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);

        await _auditLogRepository.SaveChangesAsync(cancellationToken);
    }

    private static CommandStatus ValidateCommandResultStatus(DeviceCommandResultRequest request)
    {
        if (!Enum.TryParse<CommandStatus>(request.Status, true, out var requestedStatus))
        {
            throw new InvalidOperationException("Invalid command result status.");
        }

        if (requestedStatus is not CommandStatus.Completed and not CommandStatus.Failed)
        {
            throw new InvalidOperationException("Command result status must be Completed or Failed.");
        }

        if (request.Success && requestedStatus != CommandStatus.Completed)
        {
            throw new InvalidOperationException("Success=true requires status Completed.");
        }

        if (!request.Success && requestedStatus != CommandStatus.Failed)
        {
            throw new InvalidOperationException("Success=false requires status Failed.");
        }

        return requestedStatus;
    }

    private static CommandCompletedEvent ToCommandCompletedEvent(
    RobotCommand command,
    CommandResult result)
    {
        return new CommandCompletedEvent
        {
            CommandId = command.Id,
            RobotId = command.RobotId,
            CommandType = command.CommandType.ToString(),
            CommandStatus = command.Status.ToString(),
            Success = result.Success,
            Message = result.Message,
            CompletedAt = result.CompletedAt
        };
    }

    private static DeviceCommandResultResponse ToCommandResultResponse(
        RobotCommand command,
        CommandResult result,
        bool isDuplicate)
    {
        return new DeviceCommandResultResponse
        {
            CommandId = command.Id,
            RobotId = command.RobotId,
            CommandStatus = command.Status.ToString(),
            Success = result.Success,
            Message = result.Message,
            CompletedAt = result.CompletedAt,
            IsDuplicate = isDuplicate
        };
    }
    private static RobotStatusChangedEvent CreateOnlineStatusChangedEvent(
    Guid robotId,
    DateTimeOffset changedAt)
    {
        return new RobotStatusChangedEvent
        {
            RobotId = robotId,
            Status = RobotStatus.Online.ToString(),
            IsOnline = true,
            ChangedAt = changedAt,
            LastSeenAt = changedAt
        };
    }
    private static DeviceCommandPendingResponse ToPendingResponse(RobotCommand command)
    {
        return new DeviceCommandPendingResponse
        {
            CommandId = command.Id,
            RobotId = command.RobotId,
            CommandType = command.CommandType.ToString(),
            Payload = string.IsNullOrWhiteSpace(command.PayloadJson)
                ? null
                : JsonSerializer.Deserialize<JsonElement>(command.PayloadJson),
            CreatedAt = command.CreatedAt
        };
    }
}
