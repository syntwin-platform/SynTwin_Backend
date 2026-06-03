namespace Syntwin.Application.Robots.Dtos;

public sealed class RobotLatestStateResponse
{
    public Guid RobotId { get; set; }

    public bool IsOnline { get; set; }

    public string Status { get; set; } = string.Empty;

    public TcpPoseDto? TcpPose { get; set; }

    public IReadOnlyList<double> JointAngles { get; set; } = Array.Empty<double>();

    public double? Temperature { get; set; }

    public bool? CollisionWarning { get; set; }

    public DateTimeOffset? LastSeenAt { get; set; }

    public DateTimeOffset? Timestamp { get; set; }

    public string Source { get; set; } = "SqlFallback";
}
