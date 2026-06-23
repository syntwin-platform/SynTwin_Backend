using Microsoft.AspNetCore.SignalR;
using Syntwin.Api.Hubs;
using Syntwin.Application.Realtime.Dtos;
using Syntwin.Application.Realtime.Interfaces;
using Syntwin.Application.Robots.Dtos;
namespace Syntwin.Api.Realtime;

public sealed class SignalRRobotRealtimeNotifier : IRobotRealtimeNotifier
{
    private readonly IHubContext<TelemetryHub> _hubContext;

    public SignalRRobotRealtimeNotifier(IHubContext<TelemetryHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task NotifyTelemetryUpdatedAsync(
       RobotLatestStateResponse state,
       CancellationToken cancellationToken = default)
    {
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
}