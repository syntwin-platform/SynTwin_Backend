using Syntwin.Application.Robots.Dtos;

namespace Syntwin.Application.Telemetry.Dtos;

public sealed class RobotTelemetryHistoryPoint
{
    public DateTimeOffset Timestamp { get; set; }

    public IReadOnlyList<double> JointAngles { get; set; } = Array.Empty<double>();

    public TcpPoseDto? TcpPose { get; set; }

    public double? Temperature { get; set; }

    public bool? CollisionWarning { get; set; }

    public string? Status { get; set; }

    public string? Source { get; set; }
}