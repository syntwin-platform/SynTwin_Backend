namespace Syntwin.Application.RobotSafety.Policies;

public sealed class RobotJointLimit
{
    public int Joint { get; set; }

    public double MinDeg { get; set; }

    public double MaxDeg { get; set; }
}