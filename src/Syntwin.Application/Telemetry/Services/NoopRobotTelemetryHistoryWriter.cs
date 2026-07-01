using Syntwin.Application.Telemetry.Dtos;
using Syntwin.Application.Telemetry.Interfaces;

namespace Syntwin.Application.Telemetry.Services;

public sealed class NoopRobotTelemetryHistoryWriter : IRobotTelemetryHistoryWriter
{
    public Task WriteAsync(
        RobotTelemetryHistoryWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}