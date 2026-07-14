using Syntwin.Application.Telemetry.Dtos;

namespace Syntwin.Application.Telemetry.Interfaces;

public interface IRobotTelemetryHistoryWriter
{
    Task WriteAsync(
        RobotTelemetryHistoryWriteRequest request,
        CancellationToken cancellationToken = default);
}