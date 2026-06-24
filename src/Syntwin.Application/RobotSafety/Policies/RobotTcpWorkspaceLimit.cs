namespace Syntwin.Application.RobotSafety.Policies;

public sealed class RobotTcpWorkspaceLimit
{
    public double MinX { get; set; }

    public double MaxX { get; set; }

    public double MinY { get; set; }

    public double MaxY { get; set; }

    public double MinZ { get; set; }

    public double MaxZ { get; set; }

    public double MinRotationDeg { get; set; } = -360;

    public double MaxRotationDeg { get; set; } = 360;
}