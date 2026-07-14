namespace Syntwin.Application.Devices.Dtos;

public sealed class DeviceFactoryRunStartedResponse
{
    public Guid FactoryRunId { get; set; }

    public Guid TargetId { get; set; }

    public Guid CommandId { get; set; }

    public Guid RobotId { get; set; }

    public DateTimeOffset ActualStartedAtUtc { get; set; }

    public int StartLateByMs { get; set; }

    public int? ActualStartSkewMs { get; set; }
}