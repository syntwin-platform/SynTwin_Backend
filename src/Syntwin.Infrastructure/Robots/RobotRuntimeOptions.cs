namespace Syntwin.Infrastructure.Robots;

public sealed class RobotRuntimeOptions
{
    public int OnlineTtlSeconds { get; set; } = 10;

    public int LatestStateTtlSeconds { get; set; } = 30;

    public int SqlStatusUpdateIntervalSeconds { get; set; } = 10;
}