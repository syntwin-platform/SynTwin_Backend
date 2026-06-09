using Syntwin.Application.Realtime.Dtos;
using Syntwin.Application.Robots.Dtos;

namespace Syntwin.Application.Realtime.Interfaces;

public interface IRobotRealtimeNotifier
{
    Task NotifyTelemetryUpdatedAsync(
        RobotLatestStateResponse state,
        CancellationToken cancellationToken = default);

    Task NotifyRobotStatusChangedAsync(
        RobotStatusChangedEvent statusChanged,
        CancellationToken cancellationToken = default);

    Task NotifyCommandCompletedAsync(
    CommandCompletedEvent commandCompleted,
    CancellationToken cancellationToken = default);

    Task NotifyProgramUpdatedAsync(
    ProgramUpdatedEvent programUpdated,
    CancellationToken cancellationToken = default);
}