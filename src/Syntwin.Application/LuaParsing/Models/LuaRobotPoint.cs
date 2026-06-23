namespace Syntwin.Application.LuaParsing.Models;

public sealed class LuaRobotPoint
{
    public string Name { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty; // joint | tcp

    public string Unit { get; set; } = string.Empty; // deg | mm/deg

    public double[]? JointAngles { get; set; }

    public LuaTcpPose? TcpPose { get; set; }
}