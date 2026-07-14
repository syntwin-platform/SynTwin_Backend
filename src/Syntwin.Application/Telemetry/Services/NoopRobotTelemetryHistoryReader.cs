using Syntwin.Application.Telemetry.Dtos;
using Syntwin.Application.Telemetry.Interfaces;

namespace Syntwin.Application.Telemetry.Services;

public sealed class NoopRobotTelemetryHistoryReader : IRobotTelemetryHistoryReader
{
    public Task<IReadOnlyList<RobotTelemetryHistoryPoint>> QueryAsync(
        RobotTelemetryHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<RobotTelemetryHistoryPoint>>(
            Array.Empty<RobotTelemetryHistoryPoint>());
    }
}