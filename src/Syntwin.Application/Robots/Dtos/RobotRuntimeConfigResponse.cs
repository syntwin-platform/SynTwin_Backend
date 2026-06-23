namespace Syntwin.Application.Robots.Dtos;

public sealed class RobotRuntimeConfigResponse
{
    public Guid RobotId { get; set; }

    public string RobotModel { get; set; } = string.Empty;

    public string Profile { get; set; } = "Simulator";

    public RobotMotionPolicyResponse MotionPolicy { get; set; } = new();

    public IReadOnlyList<JointLimitResponse> JointLimits { get; set; } = Array.Empty<JointLimitResponse>();
}

public sealed class RobotMotionPolicyResponse
{
    public MoveLPolicyResponse MoveL { get; set; } = new();

    public MoveJPolicyResponse MoveJ { get; set; } = new();
}

public sealed class MoveLPolicyResponse
{
    public double MaxDistanceMm { get; set; }

    public double MaxRotationDeg { get; set; }

    public double WaypointSpacingMm { get; set; }

    public int TimeoutMs { get; set; }
}

public sealed class MoveJPolicyResponse
{
    public int TimeoutMs { get; set; }

    public double MaxJointDeltaDeg { get; set; }
}

public sealed class JointLimitResponse
{
    public int Joint { get; set; }

    public double MinDeg { get; set; }

    public double MaxDeg { get; set; }
}