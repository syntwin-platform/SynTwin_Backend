using System.Text.Json;
using Syntwin.Application.Commands.Interfaces;
using Syntwin.Application.Common.Interfaces;
using Syntwin.Application.Devices.Dtos;
using Syntwin.Application.Devices.Interfaces;
using Syntwin.Application.Robots.Interfaces;
using Syntwin.Domain.Entities;
using Syntwin.Domain.Enums;
using Syntwin.Application.Robots.Dtos;

namespace Syntwin.Application.Devices.Services;

public sealed class DeviceGatewayService : IDeviceGatewayService
{
    private static readonly TimeSpan SqlStatusUpdateInterval = TimeSpan.FromSeconds(10);

    private readonly IRobotRepository _robotRepository;
    private readonly IRobotCommandRepository _commandRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRobotStateCache _robotStateCache;

    public DeviceGatewayService(
        IRobotRepository robotRepository,
        IRobotCommandRepository commandRepository,
        IPasswordHasher passwordHasher,
        IRobotStateCache robotStateCache)
    {
        _robotRepository = robotRepository;
        _commandRepository = commandRepository;
        _passwordHasher = passwordHasher;
        _robotStateCache = robotStateCache;
    }

    public async Task<bool?> HeartbeatAsync(
        Guid robotId,
        string deviceSecret,
        CancellationToken cancellationToken = default)
    {
        var auth = await AuthenticateAsync(robotId, deviceSecret, cancellationToken);

        if (auth is null)
        {
            return null;
        }

        if (auth.Status == RobotStatus.Disabled)
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;

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
        }

        return true;
    }

    public async Task<bool?> SubmitTelemetryAsync(
    Guid robotId,
    string deviceSecret,
    DeviceTelemetryRequest request,
    CancellationToken cancellationToken = default)
    {
        var auth = await AuthenticateAsync(robotId, deviceSecret, cancellationToken);

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
        var timestamp = request.Timestamp ?? now;

        await _robotStateCache.SetOnlineAsync(robotId, now, cancellationToken);

        await _robotStateCache.SetLatestAsync(new RobotLatestStateResponse
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
        }, cancellationToken);

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
        }

        return true;
    }

    public async Task<DevicePendingCommandResult> TakePendingCommandAsync(
        Guid robotId,
        string deviceSecret,
        CancellationToken cancellationToken = default)
    {
        var auth = await AuthenticateAsync(robotId, deviceSecret, cancellationToken);

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

        var command = await _commandRepository.TakeOldestPendingAsync(robotId, cancellationToken);

        return new DevicePendingCommandResult
        {
            IsAuthenticated = true,
            Command = command is null ? null : ToPendingResponse(command)
        };
    }

    public async Task<bool?> SubmitCommandResultAsync(
        Guid robotId,
        string deviceSecret,
        DeviceCommandResultRequest request,
        CancellationToken cancellationToken = default)
    {
        var auth = await AuthenticateAsync(robotId, deviceSecret, cancellationToken);
        if (auth is null || auth.Status == RobotStatus.Disabled) return auth is null ? null : false;

        if (request.RobotId != robotId)
        {
            throw new InvalidOperationException("RobotId in body does not match device header.");
        }

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
            return true;
        }

        var completedAt = request.CompletedAt ?? DateTimeOffset.UtcNow;

        command.Status = request.Success ? CommandStatus.Completed : CommandStatus.Failed;
        command.CompletedAt = completedAt;
        command.FailureReason = request.Success ? null : request.Message;

        await _commandRepository.AddCommandResultAsync(new CommandResult
        {
            CommandId = command.Id,
            RobotId = robotId,
            Success = request.Success,
            Message = request.Message,
            RawPayloadJson = request.RawPayload?.GetRawText(),
            CompletedAt = completedAt
        }, cancellationToken);

        await _commandRepository.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<Robot?> AuthenticateAsync(
        Guid robotId,
        string deviceSecret,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(deviceSecret))
        {
            return null;
        }

        var robot = await _robotRepository.GetByIdAsync(robotId, cancellationToken);

        if (robot?.DeviceTokenHash is null)
        {
            return null;
        }

        return _passwordHasher.Verify(deviceSecret, robot.DeviceTokenHash)
            ? robot
            : null;
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
