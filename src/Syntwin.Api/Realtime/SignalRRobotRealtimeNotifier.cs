using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Syntwin.Api.Hubs;
using Syntwin.Application.Realtime.Dtos;
using Syntwin.Application.Realtime.Interfaces;
using Syntwin.Application.Robots.Dtos;
using Syntwin.Infrastructure.Robots;

namespace Syntwin.Api.Realtime;

public sealed class SignalRRobotRealtimeNotifier : IRobotRealtimeNotifier
{
    private static readonly ConcurrentDictionary<Guid, DateTimeOffset> LastTelemetryBroadcastAt = new();

    private readonly IHubContext<TelemetryHub> _hubContext;
    private readonly TimeSpan _telemetryBroadcastMinInterval;

    public SignalRRobotRealtimeNotifier(
        IHubContext<TelemetryHub> hubContext,
        IOptions<RobotRuntimeOptions> options)
    {
        _hubContext = hubContext;

        var minIntervalMilliseconds = Math.Max(
            0,
            options.Value.TelemetryBroadcastMinIntervalMilliseconds);

        _telemetryBroadcastMinInterval = TimeSpan.FromMilliseconds(minIntervalMilliseconds);
    }

    public Task NotifyTelemetryUpdatedAsync(
        RobotLatestStateResponse state,
        CancellationToken cancellationToken = default)
    {
        if (!ShouldBroadcastTelemetry(state.RobotId))
        {
            return Task.CompletedTask;
        }

        return _hubContext.Clients
            .Group(TelemetryHub.GetRobotGroupName(state.RobotId))
            .SendAsync("TelemetryUpdated", state, cancellationToken);
    }

    public Task NotifyRobotStatusChangedAsync(
        RobotStatusChangedEvent statusChanged,
        CancellationToken cancellationToken = default)
    {
        return _hubContext.Clients
            .Group(TelemetryHub.GetRobotGroupName(statusChanged.RobotId))
            .SendAsync("RobotStatusChanged", statusChanged, cancellationToken);
    }

    public Task NotifyCommandCompletedAsync(
        CommandCompletedEvent commandCompleted,
        CancellationToken cancellationToken = default)
    {
        return _hubContext.Clients
            .Group(TelemetryHub.GetRobotGroupName(commandCompleted.RobotId))
            .SendAsync("CommandCompleted", commandCompleted, cancellationToken);
    }

    public Task NotifyProgramUpdatedAsync(
    ProgramUpdatedEvent programUpdated,
    CancellationToken cancellationToken = default)
    {
        return _hubContext.Clients
            .Group(TelemetryHub.GetRobotGroupName(programUpdated.RobotId))
            .SendAsync("ProgramUpdated", programUpdated, cancellationToken);
    }
    private bool ShouldBroadcastTelemetry(Guid robotId)
    {
        if (_telemetryBroadcastMinInterval <= TimeSpan.Zero)
        {
            return true;
        }

        var now = DateTimeOffset.UtcNow;

        while (true)
        {
            if (!LastTelemetryBroadcastAt.TryGetValue(robotId, out var lastBroadcastAt))
            {
                if (LastTelemetryBroadcastAt.TryAdd(robotId, now))
                {
                    return true;
                }

                continue;
            }

            if (now - lastBroadcastAt < _telemetryBroadcastMinInterval)
            {
                return false;
            }

            if (LastTelemetryBroadcastAt.TryUpdate(robotId, now, lastBroadcastAt))
            {
                return true;
            }
        }
    }
}