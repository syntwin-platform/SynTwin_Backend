namespace Syntwin.Application.RobotSafety.Policies;

public sealed class RobotSafetyPolicyDefinition
{
    public string Name { get; set; } = "Default";

    public string RobotModel { get; set; } = "Default";

    public IReadOnlyList<RobotJointLimit> JointLimits { get; set; }
        = Array.Empty<RobotJointLimit>();

    public RobotTcpWorkspaceLimit TcpWorkspace { get; set; } = new();

    public int MinSpeedPercent { get; set; } = 1;

    public int MaxSpeedPercent { get; set; } = 100;

    public int MinAccelerationPercent { get; set; } = 1;

    public int MaxAccelerationPercent { get; set; } = 100;

    public double MaxJointDeltaDegPerStep { get; set; } = 120;

    public double MaxFirstStepJointDeltaDeg { get; set; } = 120;
}