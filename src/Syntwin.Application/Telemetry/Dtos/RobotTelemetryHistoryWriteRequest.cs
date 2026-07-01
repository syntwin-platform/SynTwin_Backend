using Syntwin.Application.Robots.Dtos;

namespace Syntwin.Application.Telemetry.Dtos;

public sealed class RobotTelemetryHistoryWriteRequest
{
    public Guid RobotId { get; set; }

    public Guid? CompanyId { get; set; }

    public Guid? RuntimeSessionId { get; set; }

    public string? Model { get; set; }

    public string Source { get; set; } = "Device";

    public string Status { get; set; } = string.Empty;

    public TcpPoseDto? TcpPose { get; set; }

    public IReadOnlyList<double> JointAngles { get; set; } = Array.Empty<double>();

    public double? Temperature { get; set; }

    public bool? CollisionWarning { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    public DateTimeOffset ReceivedAt { get; set; }
}