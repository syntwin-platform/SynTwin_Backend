using Syntwin.Application.Telemetry.Dtos;

namespace Syntwin.Application.Telemetry.Interfaces;

public interface IRobotTelemetryHistoryReader
{
    Task<IReadOnlyList<RobotTelemetryHistoryPoint>> QueryAsync(
        RobotTelemetryHistoryQuery query,
        CancellationToken cancellationToken = default);
}