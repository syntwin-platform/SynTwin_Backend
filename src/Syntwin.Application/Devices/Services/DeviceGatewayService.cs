using Microsoft.Extensions.Options;
using Syntwin.Application.AuditLogs.Interfaces;
using Syntwin.Application.Commands.Interfaces;
using Syntwin.Application.Common.Interfaces;
using Syntwin.Application.Devices.Dtos;
using Syntwin.Application.Devices.Interfaces;
using Syntwin.Application.FactoryRuns.Interfaces;
using Syntwin.Application.Realtime.Dtos;
using Syntwin.Application.Realtime.Interfaces;
using Syntwin.Application.Robots.Dtos;
using Syntwin.Application.Robots.Interfaces;
using Syntwin.Application.Robots.Options;
using Syntwin.Application.Telemetry.Dtos;
using Syntwin.Application.Telemetry.Interfaces;
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
    private readonly IFactoryRunRepository _factoryRunRepository;
    private readonly IRobotCommandQueue _commandQueue;
    private readonly IRobotCommandTimeoutScheduler _commandTimeoutScheduler;
    private readonly IRobotBusyLock _robotBusyLock;
    private readonly IDistributedLock _distributedLock;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRobotStateCache _robotStateCache;
    private readonly IRobotRealtimeNotifier _realtimeNotifier;
    private readonly TimeSpan _pendingCommandPollInterval;
    private readonly int _pendingCommandMaxWaitSeconds;
    private readonly int _pendingCommandMaxSkippedQueueItems;
    private readonly IRobotRuntimeMetrics _metrics;
    private readonly IRobotTelemetryHistoryWriter _telemetryHistoryWriter;
    public DeviceGatewayService(
       IRobotRepository robotRepository,
    IRobotRuntimeSessionRepository runtimeSessionRepository,
IRobotCommandRepository commandRepository,
IFactoryRunRepository factoryRunRepository,
IRobotCommandQueue commandQueue,
IRobotCommandTimeoutScheduler commandTimeoutScheduler,
IRobotBusyLock robotBusyLock,
IDistributedLock distributedLock,
IAuditLogRepository auditLogRepository,
    IPasswordHasher passwordHasher,
    IRobotStateCache robotStateCache,
    IRobotRealtimeNotifier realtimeNotifier,
    IOptions<RobotRuntimeOptions> options,
    IRobotRuntimeMetrics metrics,
    IRobotTelemetryHistoryWriter telemetryHistoryWriter)
    {
        _robotRepository = robotRepository;
        _runtimeSessionRepository = runtimeSessionRepository;
        _commandRepository = commandRepository;
        _factoryRunRepository = factoryRunRepository;
        _commandQueue = commandQueue;
        _commandTimeoutScheduler = commandTimeoutScheduler;
        _robotBusyLock = robotBusyLock;
        _distributedLock = distributedLock;
        _auditLogRepository = auditLogRepository;
        _passwordHasher = passwordHasher;
        _robotStateCache = robotStateCache;
        _realtimeNotifier = realtimeNotifier;
        _metrics = metrics;
        _telemetryHistoryWriter = telemetryHistoryWriter;
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

        var shouldBroadcastOnline = await MarkRuntimeOnlineAsync(
            robot,
            now,
            cancellationToken);

        if (shouldBroadcastOnline)
        {
            await _realtimeNotifier.NotifyRobotStatusChangedAsync(
                CreateOnlineStatusChangedEvent(robot.Id, now),
                cancellationToken);
        }

        var runtimeSessionId = await _robotStateCache.GetCurrentRuntimeSessionIdAsync(
            robot.Id,
            cancellationToken);

        return new DeviceSessionResponse
        {
            RobotId = robot.Id,
            RuntimeSessionId = runtimeSessionId,
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
        await WriteTelemetryHistorySafeAsync(
    new RobotTelemetryHistoryWriteRequest
    {
        RobotId = auth.Id,
        CompanyId = auth.CompanyId,
        RuntimeSessionId = await _robotStateCache.GetCurrentRuntimeSessionIdAsync(
            auth.Id,
            cancellationToken),
        Model = auth.Model,
        Source = "DeviceLegacySecret",
        Status = latestState.Status,
        TcpPose = latestState.TcpPose,
        JointAngles = latestState.JointAngles,
        Temperature = latestState.Temperature,
        CollisionWarning = latestState.CollisionWarning,
        Timestamp = timestamp,
        ReceivedAt = now
    },
    cancellationToken);

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
        await WriteTelemetryHistorySafeAsync(
            new RobotTelemetryHistoryWriteRequest
            {
                RobotId = robotId,
                RuntimeSessionId = await _robotStateCache.GetCurrentRuntimeSessionIdAsync(
                    robotId,
                    cancellationToken),
                Source = "DeviceSession",
                Status = latestState.Status,
                TcpPose = latestState.TcpPose,
                JointAngles = latestState.JointAngles,
                Temperature = latestState.Temperature,
                CollisionWarning = latestState.CollisionWarning,
                Timestamp = timestamp,
                ReceivedAt = now
            },
            cancellationToken);


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
    private async Task WriteTelemetryHistorySafeAsync(
        RobotTelemetryHistoryWriteRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _telemetryHistoryWriter.WriteAsync(request, cancellationToken);
        }
        catch
        {
            // Telemetry history must never break latest-state or realtime flow.
        }
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

    public async Task<DeviceFactoryRunArmSubmitResult> ArmFactoryRunCommandWithSessionAsync(
    string accessToken,
    DeviceFactoryRunArmRequest request,
    string? ipAddress = null,
    CancellationToken cancellationToken = default)
    {
        var session = await AuthenticateSessionAsync(
            accessToken,
            "factory-runs/armed",
            ipAddress,
            cancellationToken);

        if (session is null)
        {
            return new DeviceFactoryRunArmSubmitResult
            {
                IsAuthenticated = false
            };
        }

        return await ArmFactoryRunCommandAuthenticatedAsync(
            session.RobotId,
            request,
            cancellationToken);
    }

    private async Task<DeviceFactoryRunStartedSubmitResult>
    ReportFactoryRunStartedAuthenticatedAsync(
        Guid robotId,
        DeviceFactoryRunStartedRequest request,
        CancellationToken cancellationToken)
    {
        if (request.RobotId != robotId)
        {
            throw new InvalidOperationException(
                "RobotId in body does not match authenticated device.");
        }

        await using var factoryRunLock = await AcquireFactoryRunArmLockAsync(
            request.FactoryRunId,
            cancellationToken);

        var factoryRun = await _factoryRunRepository.GetByIdForArmAsync(
            request.FactoryRunId,
            cancellationToken);

        if (factoryRun is null)
        {
            throw new InvalidOperationException("Factory run not found.");
        }

        var target = factoryRun.Targets.FirstOrDefault(
            item => item.Id == request.TargetId);

        if (target is null)
        {
            throw new InvalidOperationException("Factory run target not found.");
        }

        if (target.RobotId != robotId)
        {
            throw new InvalidOperationException(
                "Factory run target does not belong to authenticated robot.");
        }

        if (target.CommandId != request.CommandId)
        {
            throw new InvalidOperationException(
                "Factory run command does not match target command.");
        }

        var isSynchronized =
            factoryRun.CoordinationMode == FactoryCoordinationMode.Synchronized;

        if (isSynchronized && !factoryRun.ScheduledStartAtUtc.HasValue)
        {
            throw new InvalidOperationException(
                "Factory run has no scheduled start time.");
        }

        var command = await _commandRepository.GetByIdForRobotAsync(
            request.CommandId,
            robotId,
            cancellationToken);

        if (command is null ||
            command.CommandType != RobotCommandType.RunProgram)
        {
            throw new InvalidOperationException(
                "RunProgram command was not found.");
        }

        var serverNow = DateTimeOffset.UtcNow;
        var requestedActual = request.ActualStartedAtUtc ?? serverNow;

        // Không chấp nhận clock client lệch quá xa.
        var actualStartedAtUtc =
            Math.Abs((requestedActual - serverNow).TotalSeconds) <= 30
                ? requestedActual
                : serverNow;
        double? startLateByMsMetric = null;
        double? startSkewMsMetric = null;

        if (!target.ActualStartedAtUtc.HasValue)
        {
            target.ActualStartedAtUtc = actualStartedAtUtc;

            if (isSynchronized)
            {
                var lateByMs = Math.Max(
                    0,
                    (actualStartedAtUtc - factoryRun.ScheduledStartAtUtc!.Value)
                        .TotalMilliseconds);

                target.StartLateByMs = (int)Math.Min(
                    int.MaxValue,
                    Math.Round(lateByMs));

                startLateByMsMetric = target.StartLateByMs.Value;
            }
            else
            {
                target.StartLateByMs = null;
            }

            target.UpdatedAtUtc = serverNow;
        }

        if (
            !isSynchronized &&
            factoryRun.Status is
                FactoryRunStatus.Starting or
                FactoryRunStatus.Running or
                FactoryRunStatus.RunningDegraded)
        {
            target.StartedAtUtc ??= actualStartedAtUtc;

            if (target.Status is
                FactoryRunTargetStatus.Starting or
                FactoryRunTargetStatus.Armed)
            {
                target.Status = FactoryRunTargetStatus.Running;
            }

            factoryRun.StartedAtUtc ??= actualStartedAtUtc;

            var hasIsolatedTarget = factoryRun.Targets.Any(item =>
                item.Status is
                    FactoryRunTargetStatus.Failed or
                    FactoryRunTargetStatus.Cancelled);

            factoryRun.Status = hasIsolatedTarget
                ? FactoryRunStatus.RunningDegraded
                : FactoryRunStatus.Running;
            factoryRun.FailureReason = hasIsolatedTarget
                ? "One or more independent targets were isolated."
                : null;
        }

        var startParticipants = factoryRun.Targets
            .Where(item => item.Status is not (
                FactoryRunTargetStatus.Failed or FactoryRunTargetStatus.Cancelled))
            .ToList();
        var actualStarts = startParticipants
            .Where(item => item.ActualStartedAtUtc.HasValue)
            .Select(item => item.ActualStartedAtUtc!.Value)
            .ToList();

        var shouldRecordStartSkew = !factoryRun.ActualStartSkewMs.HasValue;

        if (
            isSynchronized &&
            startParticipants.Count > 0 &&
            actualStarts.Count == startParticipants.Count)
        {
            var skewMs = (
                actualStarts.Max() -
                actualStarts.Min()).TotalMilliseconds;

            factoryRun.ActualStartSkewMs = (int)Math.Min(
                int.MaxValue,
                Math.Max(0, Math.Round(skewMs)));

            if (shouldRecordStartSkew)
            {
                startSkewMsMetric = factoryRun.ActualStartSkewMs.Value;
            }
        }

        factoryRun.UpdatedAtUtc = serverNow;

        await _factoryRunRepository.SaveChangesAsync(cancellationToken);

        if (startLateByMsMetric.HasValue)
        {
            _metrics.RecordFactoryRunActualStart(
                startParticipants.Count,
                startLateByMsMetric.Value);
        }

        if (startSkewMsMetric.HasValue)
        {
            _metrics.RecordFactoryRunStartSkew(
                startParticipants.Count,
                startSkewMsMetric.Value);
        }

        return new DeviceFactoryRunStartedSubmitResult
        {
            IsAuthenticated = true,
            Response = new DeviceFactoryRunStartedResponse
            {
                FactoryRunId = factoryRun.Id,
                TargetId = target.Id,
                CommandId = request.CommandId,
                RobotId = robotId,
                ActualStartedAtUtc =
                    target.ActualStartedAtUtc ?? actualStartedAtUtc,
                StartLateByMs = target.StartLateByMs ?? 0,
                ActualStartSkewMs = factoryRun.ActualStartSkewMs
            }
        };
    }

    private async Task<DeviceFactoryRunArmSubmitResult> ArmFactoryRunCommandAuthenticatedAsync(
        Guid robotId,
        DeviceFactoryRunArmRequest request,
        CancellationToken cancellationToken)
    {
        if (request.RobotId != robotId)
        {
            throw new InvalidOperationException("RobotId in body does not match authenticated device.");
        }

        await using var factoryRunLock = await AcquireFactoryRunArmLockAsync(
            request.FactoryRunId,
            cancellationToken);

        var factoryRun = await _factoryRunRepository.GetByIdForArmAsync(
            request.FactoryRunId,
            cancellationToken);
        if (factoryRun is null)
        {
            throw new InvalidOperationException("Factory run not found.");
        }

        if (factoryRun.CoordinationMode != FactoryCoordinationMode.Synchronized)
        {
            throw new InvalidOperationException(
                "ParallelIndependent factory runs do not use the synchronized arm barrier.");
        }

        var target = factoryRun.Targets.FirstOrDefault(item => item.Id == request.TargetId);

        if (target is null)
        {
            throw new InvalidOperationException("Factory run target not found.");
        }

        if (target.RobotId != robotId)
        {
            throw new InvalidOperationException("Factory run target does not belong to authenticated robot.");
        }

        if (target.CommandId != request.CommandId)
        {
            throw new InvalidOperationException("Factory run command does not match target command.");
        }

        var command = await _commandRepository.GetByIdForRobotAsync(
            request.CommandId,
            robotId,
            cancellationToken);

        if (command is null)
        {
            throw new InvalidOperationException("Command not found.");
        }

        if (command.CommandType != RobotCommandType.RunProgram)
        {
            throw new InvalidOperationException("Only RunProgram commands can arm a factory run target.");
        }

        if (command.Status is CommandStatus.Failed or CommandStatus.Timeout or CommandStatus.Cancelled)
        {
            throw new InvalidOperationException($"Command is already terminal: {command.Status}.");
        }

        var now = DateTimeOffset.UtcNow;
        var targetAlreadyArmed = target.Status is FactoryRunTargetStatus.Armed or FactoryRunTargetStatus.Running;
        var hasStateChange = false;
        var commandTimeoutExtended = false;
        (double ArmSpreadMs, double LeadTimeMs)? barrierReadyMetric = null;

        if (!targetAlreadyArmed)
        {
            target.CommandReceivedAtUtc ??= request.ReceivedAtUtc ?? now;
            target.ArmedAtUtc ??= request.ArmedAtUtc ?? now;
            target.RuntimeSessionId ??= await _robotStateCache.GetCurrentRuntimeSessionIdAsync(
                robotId,
                cancellationToken);

            target.EstimatedStepDurationsJson = JsonSerializer.Serialize(
                request.EstimatedStepDurationsMs.Select(duration => Math.Max(0, duration)).ToArray());

            var estimatedProgramDurationMs = request.EstimatedStepDurationsMs
                .Select(duration => Math.Max(0, duration))
                .Aggregate(0L, (total, duration) => total + duration);
            var estimatedTimeoutAt = now
                .AddMilliseconds(estimatedProgramDurationMs)
                .AddMinutes(2);

            if (!command.TimeoutAt.HasValue || command.TimeoutAt < estimatedTimeoutAt)
            {
                command.TimeoutAt = estimatedTimeoutAt;
                commandTimeoutExtended = true;
            }

            if (target.Status == FactoryRunTargetStatus.Starting)
            {
                target.Status = FactoryRunTargetStatus.Armed;
            }

            target.FailureReason = null;
            target.UpdatedAtUtc = now;
            hasStateChange = true;
        }

        var participatingTargets = factoryRun.FailurePolicy == FactoryFailurePolicy.IsolateTarget
            ? factoryRun.Targets
                .Where(item => item.Status is not (
                    FactoryRunTargetStatus.Failed or FactoryRunTargetStatus.Cancelled))
                .ToList()
            : factoryRun.Targets.ToList();
        var allTargetsArmed = participatingTargets.Count > 0 && participatingTargets.All(item =>
            item.Status is FactoryRunTargetStatus.Armed or FactoryRunTargetStatus.Running);

        if (allTargetsArmed && !factoryRun.ScheduledStartAtUtc.HasValue)
        {
            var commonStepDurationsMs = BuildCommonStepDurations(factoryRun);

            var armedTimes = participatingTargets
                .Where(item => item.ArmedAtUtc.HasValue)
                .Select(item => item.ArmedAtUtc!.Value)
                .ToList();

            var armSpreadMs = armedTimes.Count > 1
                ? (armedTimes.Max() - armedTimes.Min()).TotalMilliseconds
                : 0;
            var serverAcceptedArmTimes = participatingTargets
                .Where(item => item.ArmedAtUtc.HasValue && item.UpdatedAtUtc.HasValue)
                .Select(item => item.UpdatedAtUtc!.Value)
                .ToList();
            var serverArmSpreadMs = serverAcceptedArmTimes.Count > 1
                ? (serverAcceptedArmTimes.Max() - serverAcceptedArmTimes.Min()).TotalMilliseconds
                : 0;
            var observedArmSpreadMs = Math.Max(armSpreadMs, serverArmSpreadMs);
            var startLeadTime = GetFactoryRunStartLeadTime(
                participatingTargets.Count,
                observedArmSpreadMs);
            var scheduledStartAtUtc = now.Add(startLeadTime);

            barrierReadyMetric = (
                observedArmSpreadMs,
                startLeadTime.TotalMilliseconds);

            factoryRun.ScheduledStartAtUtc = scheduledStartAtUtc;
            factoryRun.StepDurationsJson = JsonSerializer.Serialize(commonStepDurationsMs);

            factoryRun.StartedAtUtc = scheduledStartAtUtc;
            var hasIsolatedTarget = participatingTargets.Count < factoryRun.Targets.Count;
            factoryRun.Status = hasIsolatedTarget
                ? FactoryRunStatus.RunningDegraded
                : FactoryRunStatus.Running;
            factoryRun.FailureReason = hasIsolatedTarget
                ? "One or more targets were isolated before synchronized start."
                : null;
            factoryRun.UpdatedAtUtc = now;

            foreach (var armedTarget in participatingTargets)
            {
                if (armedTarget.Status == FactoryRunTargetStatus.Armed)
                {
                    armedTarget.Status = FactoryRunTargetStatus.Running;
                    armedTarget.StartedAtUtc = scheduledStartAtUtc;
                    armedTarget.FailureReason = null;
                    armedTarget.UpdatedAtUtc = now;
                }
            }

            hasStateChange = true;
        }
        else if (hasStateChange)
        {
            factoryRun.UpdatedAtUtc = now;
        }

        if (hasStateChange)
        {
            await _factoryRunRepository.SaveChangesAsync(cancellationToken);
        }

        if (commandTimeoutExtended)
        {
            await _commandTimeoutScheduler.ScheduleAsync(command, cancellationToken);
        }

        if (barrierReadyMetric.HasValue)
        {
            _metrics.RecordFactoryRunBarrierReady(
                participatingTargets.Count,
                barrierReadyMetric.Value.ArmSpreadMs,
                barrierReadyMetric.Value.LeadTimeMs);
        }

        var isReady = factoryRun.ScheduledStartAtUtc.HasValue &&
            factoryRun.Status is FactoryRunStatus.Running or FactoryRunStatus.RunningDegraded;

        _metrics.RecordFactoryRunArmPoll(isReady);

        return new DeviceFactoryRunArmSubmitResult
        {
            IsAuthenticated = true,
            Response = new DeviceFactoryRunArmResponse
            {
                FactoryRunId = factoryRun.Id,
                TargetId = target.Id,
                CommandId = request.CommandId,
                RobotId = robotId,
                IsReady = isReady,
                Status = target.Status.ToString(),
                ScheduledStartAtUtc = factoryRun.ScheduledStartAtUtc,
                ExpectedParticipantCount = participatingTargets.Count,
                StepDurationsMs = DeserializeStepDurations(factoryRun.StepDurationsJson)
            }
        };
    }

    public async Task<DeviceFactoryRunStartedSubmitResult>
    ReportFactoryRunStartedWithSessionAsync(
        string accessToken,
        DeviceFactoryRunStartedRequest request,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        var session = await AuthenticateSessionAsync(
            accessToken,
            "factory-runs/started",
            ipAddress,
            cancellationToken);

        if (session is null)
        {
            return new DeviceFactoryRunStartedSubmitResult
            {
                IsAuthenticated = false
            };
        }

        return await ReportFactoryRunStartedAuthenticatedAsync(
            session.RobotId,
            request,
            cancellationToken);
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

        if (
            command.CommandType == RobotCommandType.PrepareProgram &&
            requestedStatus is CommandStatus.Failed or CommandStatus.Timeout or CommandStatus.Cancelled)
        {
            var target = await _factoryRunRepository.GetTargetByPrepareCommandIdAsync(
                command.Id,
                cancellationToken);

            if (target is not null)
            {
                await _robotBusyLock.ReleaseAsync(
                    target.RobotId,
                    target.Id,
                    cancellationToken);
            }
        }

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

    private async Task<IDistributedLockHandle> AcquireFactoryRunArmLockAsync(
    Guid factoryRunId,
    CancellationToken cancellationToken)
    {
        var lockKey = $"factory-run:{factoryRunId:N}:arm";

        // Tối đa khoảng 5 giây. Bình thường lock chỉ giữ vài chục ms.
        for (var attempt = 0; attempt < 200; attempt++)
        {
            var handle = await _distributedLock.TryAcquireAsync(
                lockKey,
                TimeSpan.FromSeconds(10),
                cancellationToken);

            if (handle is not null)
            {
                return handle;
            }

            await Task.Delay(25, cancellationToken);
        }

        throw new InvalidOperationException(
            "Factory run barrier is busy. Please retry the arm request.");
    }

    private static TimeSpan GetFactoryRunStartLeadTime(
        int targetCount,
        double observedArmSpreadMs)
    {
        double baseLeadTimeMs;

        if (targetCount <= 1)
        {
            baseLeadTimeMs = 600;
        }
        else if (targetCount <= 5)
        {
            baseLeadTimeMs = 1000;
        }
        else if (targetCount <= 10)
        {
            baseLeadTimeMs = 1500;
        }
        else
        {
            baseLeadTimeMs = 2000;
        }

        // A slow arm cohort needs time for the already-armed devices to poll once
        // more and receive the committed epoch. Fast cohorts retain the short base
        // lead time; slow cohorts adapt without using a fixed long delay every run.
        var propagationMarginMs = Math.Max(750, targetCount * 200);
        var adaptiveLeadTimeMs = observedArmSpreadMs + propagationMarginMs;
        var finalLeadTimeMs = Math.Clamp(
            Math.Max(baseLeadTimeMs, adaptiveLeadTimeMs),
            baseLeadTimeMs,
            10000);

        return TimeSpan.FromMilliseconds(finalLeadTimeMs);
    }


    private static IReadOnlyList<int> BuildCommonStepDurations(FactoryRun factoryRun)
    {
        var estimates = factoryRun.Targets
            .Select(target => DeserializeStepDurations(target.EstimatedStepDurationsJson))
            .Where(durations => durations.Count > 0)
            .ToList();

        if (estimates.Count == 0)
        {
            return Array.Empty<int>();
        }

        var maxStepCount = estimates.Max(durations => durations.Count);
        var common = new int[maxStepCount];

        for (var index = 0; index < maxStepCount; index++)
        {
            common[index] = estimates
                .Select(durations => index < durations.Count ? durations[index] : 0)
                .DefaultIfEmpty(0)
                .Max();
        }

        return common.Select(duration => Math.Max(50, duration)).ToArray();
    }

    private static IReadOnlyList<int> DeserializeStepDurations(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<int>();
        }

        try
        {
            return JsonSerializer.Deserialize<int[]>(json) ?? Array.Empty<int>();
        }
        catch
        {
            return Array.Empty<int>();
        }
    }

    private static bool IsBusyLockCommand(RobotCommandType commandType)
    {
        return commandType is not RobotCommandType.EStop
            and not RobotCommandType.PrepareProgram;
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
