using Microsoft.Extensions.Options;
using Syntwin.Application.AuditLogs.Interfaces;
using Syntwin.Application.Commands.Interfaces;
using Syntwin.Application.Common.Interfaces;
using Syntwin.Application.Devices.Dtos;
using Syntwin.Application.Devices.Interfaces;
using Syntwin.Application.Realtime.Dtos;
using Syntwin.Application.Realtime.Interfaces;
using Syntwin.Application.Robots.Dtos;
using Syntwin.Application.Robots.Interfaces;
using Syntwin.Application.Robots.Options;
using Syntwin.Domain.Entities;
using Syntwin.Domain.Enums;
using System.Security.Cryptography;
using System.Text.Json;

namespace Syntwin.Application.Devices.Services;

public sealed class DeviceGatewayService : IDeviceGatewayService
{
    private readonly IRobotRuntimeSessionRepository _runtimeSessionRepository;
    private readonly TimeSpan _runtimeSessionTtl;
    private readonly TimeSpan _deviceSessionTtl;
    private readonly TimeSpan _telemetryBroadcastMinInterval;
    private readonly IRobotRepository _robotRepository;
    private readonly IRobotCommandRepository _commandRepository;
    private readonly IRobotCommandQueue _commandQueue;
    private readonly IRobotCommandTimeoutScheduler _commandTimeoutScheduler;
    private readonly IRobotBusyLock _robotBusyLock;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRobotStateCache _robotStateCache;
    private readonly IRobotRealtimeNotifier _realtimeNotifier;
    private readonly TimeSpan _pendingCommandPollInterval;
    private readonly int _pendingCommandMaxWaitSeconds;
    private readonly int _pendingCommandMaxSkippedQueueItems;
    private readonly IRobotRuntimeMetrics _metrics;
    public DeviceGatewayService(
       IRobotRepository robotRepository,
IRobotRuntimeSessionRepository runtimeSessionRepository,
IRobotCommandRepository commandRepository,
IRobotCommandQueue commandQueue,
IRobotCommandTimeoutScheduler commandTimeoutScheduler,
IRobotBusyLock robotBusyLock,
IAuditLogRepository auditLogRepository,
IPasswordHasher passwordHasher,
IRobotStateCache robotStateCache,
IRobotRealtimeNotifier realtimeNotifier,
IOptions<RobotRuntimeOptions> options,
IRobotRuntimeMetrics metrics)
    {
        _robotRepository = robotRepository;
        _runtimeSessionRepository = runtimeSessionRepository;
        _commandRepository = commandRepository;
        _commandQueue = commandQueue;
        _commandTimeoutScheduler = commandTimeoutScheduler;
        _robotBusyLock = robotBusyLock;
        _auditLogRepository = auditLogRepository;
        _passwordHasher = passwordHasher;
        _robotStateCache = robotStateCache;
        _realtimeNotifier = realtimeNotifier;
        _metrics = metrics;
        var runtimeSessionTtlSeconds = Math.Max(
    60,
    options.Value.RuntimeSessionTtlSeconds);

        _runtimeSessionTtl = TimeSpan.FromSeconds(runtimeSessionTtlSeconds);
        var deviceSessionTtlSeconds = Math.Max(
    60,
    options.Value.DeviceSessionTtlSeconds);

        _deviceSessionTtl = TimeSpan.FromSeconds(deviceSessionTtlSeconds);
        var telemetryBroadcastMinIntervalMilliseconds = Math.Max(
    0,
    options.Value.TelemetryBroadcastMinIntervalMilliseconds);

        _telemetryBroadcastMinInterval = TimeSpan.FromMilliseconds(
            telemetryBroadcastMinIntervalMilliseconds);
        _pendingCommandPollInterval = TimeSpan.FromMilliseconds(
    Math.Clamp(
        options.Value.PendingCommandPollIntervalMilliseconds,
        50,
        1000));

        _pendingCommandMaxWaitSeconds = Math.Clamp(
            options.Value.PendingCommandMaxWaitSeconds,
            0,
            60);

        _pendingCommandMaxSkippedQueueItems = Math.Max(
            1,
            options.Value.PendingCommandMaxSkippedQueueItems);
    }

    public async Task<DeviceSessionResponse?> CreateSessionAsync(
    Guid robotId,
    string deviceSecret,
    string? ipAddress = null,
    CancellationToken cancellationToken = default)
    {
        var robot = await AuthenticateAsync(
            robotId,
            deviceSecret,
            "session",
            ipAddress,
            cancellationToken);

        if (robot is null || robot.Status == RobotStatus.Disabled)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var token = CreateRuntimeAccessToken(robot.Id);

        var session = new DeviceRuntimeSession
        {
            RobotId = robot.Id,
            AccessToken = token,
            IssuedAt = now,
            ExpiresAt = now.Add(_deviceSessionTtl)
        };

        await _robotStateCache.SetDeviceSessionAsync(
            session,
            _deviceSessionTtl,
            cancellationToken);

        return new DeviceSessionResponse
        {
            RobotId = robot.Id,
            AccessToken = token,
            ExpiresInSeconds = (int)_deviceSessionTtl.TotalSeconds
        };
    }
    public async Task<bool?> HeartbeatWithSessionAsync(
    string accessToken,
    string? ipAddress = null,
    CancellationToken cancellationToken = default)
    {
        var session = await AuthenticateSessionAsync(
            accessToken,
            "heartbeat",
            ipAddress,
            cancellationToken);

        if (session is null)
        {
            _metrics.RecordHeartbeat(accepted: false, sessionToken: true);
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var runtimeOnline = await MarkRuntimeOnlineFromSessionAsync(
            session.RobotId,
            now,
            cancellationToken);

        if (!runtimeOnline.IsAllowed)
        {
            _metrics.RecordHeartbeat(accepted: false, sessionToken: true);
            return false;
        }

        if (runtimeOnline.ShouldBroadcastOnline)
        {
            await _realtimeNotifier.NotifyRobotStatusChangedAsync(
                CreateOnlineStatusChangedEvent(session.RobotId, now),
                cancellationToken);
        }
        _metrics.RecordHeartbeat(accepted: true, sessionToken: true);
        return true;
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
            _metrics.RecordHeartbeat(accepted: false, sessionToken: false);
            return null;
        }

        if (auth.Status == RobotStatus.Disabled)
        {
            _metrics.RecordHeartbeat(accepted: false, sessionToken: false);
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        var shouldBroadcastOnline = await MarkRuntimeOnlineAsync(
            auth,
            now,
            cancellationToken);

        if (shouldBroadcastOnline)
        {
            await _realtimeNotifier.NotifyRobotStatusChangedAsync(
                CreateOnlineStatusChangedEvent(robotId, now),
                cancellationToken);
        }
        _metrics.RecordHeartbeat(accepted: true, sessionToken: false);
        return true;
    }

    public async Task<bool?> SubmitTelemetryWithSessionAsync(
    string accessToken,
    DeviceTelemetryRequest request,
    string? ipAddress = null,
    CancellationToken cancellationToken = default)
    {
        var session = await AuthenticateSessionAsync(
            accessToken,
            "telemetry",
            ipAddress,
            cancellationToken);

        if (session is null)
        {
            _metrics.RecordTelemetry(accepted: false, sessionToken: true, broadcasted: false);
            return null;
        }

        return await SubmitTelemetrySessionAuthenticatedAsync(
            session.RobotId,
            request,
            cancellationToken);
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
            _metrics.RecordTelemetry(accepted: false, sessionToken: false, broadcasted: false);
            return null;
        }

        if (auth.Status == RobotStatus.Disabled)
        {
            _metrics.RecordTelemetry(accepted: false, sessionToken: false, broadcasted: false);
            return false;
        }

        return await SubmitTelemetryAuthenticatedAsync(
            auth,
            request,
            cancellationToken);
    }

    private async Task<bool?> SubmitTelemetryAuthenticatedAsync(
    Robot auth,
    DeviceTelemetryRequest request,
    CancellationToken cancellationToken)
    {
        if (auth.Status == RobotStatus.Disabled)
        {
            _metrics.RecordTelemetry(accepted: false, sessionToken: false, broadcasted: false);
            return false;
        }

        if (request.RobotId != auth.Id)
        {
            throw new InvalidOperationException("RobotId in body does not match authenticated device.");
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
        var shouldBroadcastOnline = await MarkRuntimeOnlineAsync(
            auth,
            now,
            cancellationToken);

        var latestState = new RobotLatestStateResponse
        {
            RobotId = auth.Id,
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

        var hasTelemetryViewers = await _robotStateCache.HasTelemetryViewersAsync(
    auth.Id,
    cancellationToken);

        var shouldBroadcastTelemetry = hasTelemetryViewers &&
            await _robotStateCache.ShouldBroadcastTelemetryAsync(
                auth.Id,
                _telemetryBroadcastMinInterval,
                cancellationToken);

        if (shouldBroadcastTelemetry)
        {
            await _realtimeNotifier.NotifyTelemetryUpdatedAsync(
                latestState,
                cancellationToken);
        }

        if (shouldBroadcastOnline)
        {
            await _realtimeNotifier.NotifyRobotStatusChangedAsync(
                CreateOnlineStatusChangedEvent(auth.Id, now),
                cancellationToken);
        }
        _metrics.RecordTelemetry(
    accepted: true,
    sessionToken: false,
    broadcasted: shouldBroadcastTelemetry);

        return true;
    }

    private async Task<bool?> SubmitTelemetrySessionAuthenticatedAsync(
Guid robotId,
DeviceTelemetryRequest request,
CancellationToken cancellationToken)
    {
        if (request.RobotId != robotId)
        {
            throw new InvalidOperationException("RobotId in body does not match authenticated device.");
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
        var runtimeOnline = await MarkRuntimeOnlineFromSessionAsync(
            robotId,
            now,
            cancellationToken);

        if (!runtimeOnline.IsAllowed)
        {
            _metrics.RecordTelemetry(accepted: false, sessionToken: true, broadcasted: false);
            return false;
        }

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

        var hasTelemetryViewers = await _robotStateCache.HasTelemetryViewersAsync(
    robotId,
    cancellationToken);

        var shouldBroadcastTelemetry = hasTelemetryViewers &&
            await _robotStateCache.ShouldBroadcastTelemetryAsync(
                robotId,
                _telemetryBroadcastMinInterval,
                cancellationToken);

        if (shouldBroadcastTelemetry)
        {
            await _realtimeNotifier.NotifyTelemetryUpdatedAsync(
                latestState,
                cancellationToken);
        }

        if (runtimeOnline.ShouldBroadcastOnline)
        {
            await _realtimeNotifier.NotifyRobotStatusChangedAsync(
                CreateOnlineStatusChangedEvent(robotId, now),
                cancellationToken);
        }
        _metrics.RecordTelemetry(
    accepted: true,
    sessionToken: true,
    broadcasted: shouldBroadcastTelemetry);

        return true;
    }

    public async Task<DevicePendingCommandResult> TakePendingCommandWithSessionAsync(
   string accessToken,
   bool isBusy = false,
   int waitSeconds = 0,
   string? ipAddress = null,
   CancellationToken cancellationToken = default)
    {
        var session = await AuthenticateSessionAsync(
            accessToken,
            "commands/pending",
            ipAddress,
            cancellationToken);

        if (session is null)
        {
            _metrics.RecordPendingCommandPoll(
                authenticated: false,
                delivered: false,
                waited: waitSeconds > 0);

            return new DevicePendingCommandResult
            {
                IsAuthenticated = false
            };
        }
        var command = await TakeQueuedPendingCommandAsync(
            session.RobotId,
            isBusy,
            waitSeconds,
            cancellationToken);
        _metrics.RecordPendingCommandPoll(
    authenticated: true,
    delivered: command is not null,
    waited: waitSeconds > 0);
        return new DevicePendingCommandResult
        {
            IsAuthenticated = true,
            Command = command is null ? null : ToPendingResponse(command)
        };
    }

    public async Task<DevicePendingCommandResult> TakePendingCommandAsync(
 Guid robotId,
 string deviceSecret,
 bool isBusy = false,
 int waitSeconds = 0,
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
            _metrics.RecordPendingCommandPoll(
                authenticated: false,
                delivered: false,
                waited: waitSeconds > 0);

            return new DevicePendingCommandResult
            {
                IsAuthenticated = false
            };
        }

        return await TakePendingCommandAuthenticatedAsync(
    auth,
    isBusy,
    waitSeconds,
    cancellationToken);
    }

    private async Task<DevicePendingCommandResult> TakePendingCommandAuthenticatedAsync(
  Robot auth,
  bool isBusy,
  int waitSeconds,
  CancellationToken cancellationToken)
    {
        if (auth.Status == RobotStatus.Disabled)
        {
            _metrics.RecordPendingCommandPoll(
                authenticated: true,
                delivered: false,
                waited: waitSeconds > 0);

            return new DevicePendingCommandResult
            {
                IsAuthenticated = true,
                IsDisabled = true
            };
        }

        var command = await TakeQueuedPendingCommandAsync(
    auth.Id,
    isBusy,
    waitSeconds,
    cancellationToken);

        _metrics.RecordPendingCommandPoll(
    authenticated: true,
    delivered: command is not null,
    waited: waitSeconds > 0);

        return new DevicePendingCommandResult
        {
            IsAuthenticated = true,
            Command = command is null ? null : ToPendingResponse(command)
        };
    }

    private async Task<RobotCommand?> TakeQueuedPendingCommandAsync(
Guid robotId,
bool safetyOnly,
int waitSeconds,
CancellationToken cancellationToken)
    {
        var clampedWaitSeconds = Math.Clamp(
    waitSeconds,
    0,
    _pendingCommandMaxWaitSeconds);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(clampedWaitSeconds);

        while (true)
        {
            for (var attempt = 0; attempt < _pendingCommandMaxSkippedQueueItems; attempt++)
            {
                var waitTimeout = attempt == 0
                    ? GetPendingCommandWaitTimeout(deadline, clampedWaitSeconds)
                    : TimeSpan.Zero;

                var commandId = waitTimeout > TimeSpan.Zero
                    ? await _commandQueue.DequeueOrWaitAsync(
                        robotId,
                        safetyOnly,
                        waitTimeout,
                        cancellationToken)
                    : await _commandQueue.DequeueAsync(
                        robotId,
                        safetyOnly,
                        cancellationToken);

                if (!commandId.HasValue)
                {
                    break;
                }

                var command = await _commandRepository.GetByIdForRobotAsync(
                    commandId.Value,
                    robotId,
                    cancellationToken);

                if (command is null || command.Status != CommandStatus.Pending)
                {
                    continue;
                }

                if (safetyOnly && command.CommandType != RobotCommandType.EStop)
                {
                    continue;
                }

                var now = DateTimeOffset.UtcNow;
                var originalStatus = command.Status;
                var originalSentAt = command.SentAt;
                var originalLastDeliveryAttemptAt = command.LastDeliveryAttemptAt;
                var originalDeliveryAttemptCount = command.DeliveryAttemptCount;

                command.Status = CommandStatus.Sent;
                command.SentAt ??= now;
                command.LastDeliveryAttemptAt = now;
                command.DeliveryAttemptCount += 1;

                try
                {
                    await _commandRepository.SaveChangesAsync(cancellationToken);
                    return command;
                }
                catch
                {
                    command.Status = originalStatus;
                    command.SentAt = originalSentAt;
                    command.LastDeliveryAttemptAt = originalLastDeliveryAttemptAt;
                    command.DeliveryAttemptCount = originalDeliveryAttemptCount;

                    await _commandQueue.RequeueAsync(command, cancellationToken);
                    throw;
                }
            }

            if (clampedWaitSeconds == 0 || DateTimeOffset.UtcNow >= deadline)
            {
                return null;
            }

            var remaining = deadline - DateTimeOffset.UtcNow;
            var delay = remaining < _pendingCommandPollInterval
    ? remaining
    : _pendingCommandPollInterval;

            if (delay <= TimeSpan.Zero)
            {
                return null;
            }

            await Task.Delay(delay, cancellationToken);
        }
    }

    private static TimeSpan GetPendingCommandWaitTimeout(
        DateTimeOffset deadline,
        int clampedWaitSeconds)
    {
        if (clampedWaitSeconds == 0)
        {
            return TimeSpan.Zero;
        }

        var remaining = deadline - DateTimeOffset.UtcNow;

        return remaining > TimeSpan.Zero
            ? remaining
            : TimeSpan.Zero;
    }

    public async Task<DeviceCommandResultSubmitResult> SubmitCommandResultWithSessionAsync(
    string accessToken,
    DeviceCommandResultRequest request,
    string? ipAddress = null,
    CancellationToken cancellationToken = default)
    {
        var session = await AuthenticateSessionAsync(
     accessToken,
     "commands/result",
     ipAddress,
     cancellationToken);

        if (session is null)
        {
            _metrics.RecordCommandResult(
                authenticated: false,
                accepted: false,
                duplicate: false);

            return new DeviceCommandResultSubmitResult
            {
                IsAuthenticated = false
            };
        }

        return await SubmitCommandResultAuthenticatedAsync(
            session.RobotId,
            request,
            ipAddress,
            cancellationToken);
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
            _metrics.RecordCommandResult(
                authenticated: false,
                accepted: false,
                duplicate: false);

            return new DeviceCommandResultSubmitResult
            {
                IsAuthenticated = false
            };
        }

        if (auth.Status == RobotStatus.Disabled)
        {
            _metrics.RecordCommandResult(
                authenticated: true,
                accepted: false,
                duplicate: false);

            return new DeviceCommandResultSubmitResult
            {
                IsAuthenticated = true,
                IsDisabled = true
            };
        }

        return await SubmitCommandResultAuthenticatedAsync(
           auth.Id,
           request,
           ipAddress,
           cancellationToken);
    }

    private async Task<DeviceCommandResultSubmitResult> SubmitCommandResultAuthenticatedAsync(
  Guid robotId,
  DeviceCommandResultRequest request,
  string? ipAddress,
  CancellationToken cancellationToken)
    {
        if (request.RobotId != robotId)
        {
            throw new InvalidOperationException("RobotId in body does not match authenticated device.");
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
            _metrics.RecordCommandResult(
                authenticated: true,
                accepted: true,
                duplicate: true);

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

        await _commandTimeoutScheduler.RemoveAsync(
            command.Id,
            cancellationToken);

        if (IsBusyLockCommand(command.CommandType))
        {
            await _robotBusyLock.ReleaseAsync(
                command.RobotId,
                command.Id,
                cancellationToken);
        }

        await _realtimeNotifier.NotifyCommandCompletedAsync(
    ToCommandCompletedEvent(command, commandResult),
    cancellationToken);

        _metrics.RecordCommandResult(
            authenticated: true,
            accepted: true,
            duplicate: false);

        return new DeviceCommandResultSubmitResult
        {
            IsAuthenticated = true,
            Result = ToCommandResultResponse(command, commandResult, isDuplicate: false)
        };
    }

    private static bool IsBusyLockCommand(RobotCommandType commandType)
    {
        return commandType != RobotCommandType.EStop;
    }

    private async Task<(bool IsAllowed, bool ShouldBroadcastOnline)> MarkRuntimeOnlineFromSessionAsync(
Guid robotId,
DateTimeOffset now,
CancellationToken cancellationToken)
    {
        var cachedSessionId = await _robotStateCache.GetCurrentRuntimeSessionIdAsync(
            robotId,
            cancellationToken);

        if (cachedSessionId.HasValue)
        {
            await _robotStateCache.SetOnlineAsync(robotId, now, cancellationToken);

            await _robotStateCache.SetCurrentRuntimeSessionAsync(
                robotId,
                cachedSessionId.Value,
                _runtimeSessionTtl,
                cancellationToken);

            return (true, false);
        }

        var robot = await _robotRepository.GetByIdAsync(robotId, cancellationToken);

        if (robot is null || robot.Status == RobotStatus.Disabled)
        {
            return (false, false);
        }

        var shouldBroadcastOnline = await MarkRuntimeOnlineAsync(
            robot,
            now,
            cancellationToken);

        return (true, shouldBroadcastOnline);
    }

    private async Task<bool> MarkRuntimeOnlineAsync(
    Robot robot,
    DateTimeOffset now,
    CancellationToken cancellationToken)
    {
        await _robotStateCache.SetOnlineAsync(robot.Id, now, cancellationToken);

        var cachedSessionId = await _robotStateCache.GetCurrentRuntimeSessionIdAsync(
            robot.Id,
            cancellationToken);

        if (cachedSessionId.HasValue && robot.Status == RobotStatus.Online)
        {
            await _robotStateCache.SetCurrentRuntimeSessionAsync(
                robot.Id,
                cachedSessionId.Value,
                _runtimeSessionTtl,
                cancellationToken);

            return false;
        }

        RobotRuntimeSession? openSession = null;

        if (cachedSessionId.HasValue)
        {
            var cachedSession = await _runtimeSessionRepository.GetByIdAsync(
                cachedSessionId.Value,
                cancellationToken);

            if (cachedSession?.EndedAt is null)
            {
                openSession = cachedSession;
            }
        }

        openSession ??= await _runtimeSessionRepository.GetOpenByRobotIdAsync(
            robot.Id,
            cancellationToken);

        if (openSession is not null)
        {
            await _robotStateCache.SetCurrentRuntimeSessionAsync(
                robot.Id,
                openSession.Id,
                _runtimeSessionTtl,
                cancellationToken);

            var shouldNotifyOnline = robot.Status != RobotStatus.Online;

            if (shouldNotifyOnline)
            {
                robot.Status = RobotStatus.Online;
                robot.LastSeenAt = now;
                robot.UpdatedAt = now;

                await _runtimeSessionRepository.SaveChangesAsync(cancellationToken);
            }

            return shouldNotifyOnline;
        }

        var runtimeSession = new RobotRuntimeSession
        {
            Id = Guid.NewGuid(),
            RobotId = robot.Id,
            StartedAt = now,
            LastSeenAt = now,
            CreatedAt = now
        };

        robot.Status = RobotStatus.Online;
        robot.LastSeenAt = now;
        robot.UpdatedAt = now;

        var createdOpenSession = await _runtimeSessionRepository.TryAddOpenSessionAsync(
            runtimeSession,
            cancellationToken);

        if (!createdOpenSession)
        {
            openSession = await _runtimeSessionRepository.GetOpenByRobotIdAsync(
                robot.Id,
                cancellationToken);

            if (openSession is null)
            {
                throw new InvalidOperationException("Unable to resolve open robot runtime session.");
            }

            await _robotStateCache.SetCurrentRuntimeSessionAsync(
                robot.Id,
                openSession.Id,
                _runtimeSessionTtl,
                cancellationToken);

            return false;
        }

        await _robotStateCache.SetCurrentRuntimeSessionAsync(
            robot.Id,
            runtimeSession.Id,
            _runtimeSessionTtl,
            cancellationToken);

        return true;
    }

    private async Task<DeviceRuntimeSession?> AuthenticateSessionAsync(
 string accessToken,
 string endpointName,
 string? ipAddress,
 CancellationToken cancellationToken)
    {
        if (!TryReadRobotIdFromAccessToken(accessToken, out var robotId))
        {
            await AddDeviceAuthFailedAuditAsync(
                robot: null,
                robotId: Guid.Empty,
                endpointName,
                ipAddress,
                cancellationToken);

            return null;
        }

        var session = await _robotStateCache.GetDeviceSessionAsync(
            robotId,
            cancellationToken);

        if (session is null ||
            session.ExpiresAt <= DateTimeOffset.UtcNow ||
            !string.Equals(session.AccessToken, accessToken, StringComparison.Ordinal))
        {
            await AddDeviceAuthFailedAuditAsync(
                robot: null,
                robotId,
                endpointName,
                ipAddress,
                cancellationToken);

            return null;
        }

        return session;
    }

    private static bool TryReadRobotIdFromAccessToken(
        string accessToken,
        out Guid robotId)
    {
        robotId = Guid.Empty;

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return false;
        }

        var separatorIndex = accessToken.IndexOf('.', StringComparison.Ordinal);

        if (separatorIndex <= 0)
        {
            return false;
        }

        var robotIdPart = accessToken[..separatorIndex];

        return Guid.TryParseExact(robotIdPart, "N", out robotId);
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

    private static string CreateRuntimeAccessToken(Guid robotId)
    {
        var randomBytes = RandomNumberGenerator.GetBytes(32);
        var randomToken = Convert.ToBase64String(randomBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        return $"{robotId:N}.{randomToken}";
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
