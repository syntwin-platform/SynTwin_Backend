namespace Syntwin.Application.Telemetry.Dtos;

public sealed class RobotTelemetryHistoryQuery
{
    public Guid RobotId { get; set; }

    public Guid? RuntimeSessionId { get; set; }

    public DateTimeOffset From { get; set; }

    public DateTimeOffset To { get; set; }

    public TimeSpan? Interval { get; set; }

    public int Limit { get; set; } = 1000;

    public IReadOnlyList<string> Fields { get; set; } = Array.Empty<string>();
}