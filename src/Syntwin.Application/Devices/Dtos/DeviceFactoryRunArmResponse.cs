namespace Syntwin.Application.Devices.Dtos;

public sealed class DeviceFactoryRunArmResponse
{
    public Guid FactoryRunId { get; set; }

    public Guid TargetId { get; set; }

    public Guid CommandId { get; set; }

    public Guid RobotId { get; set; }

    public bool IsReady { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset? ScheduledStartAtUtc { get; set; }
    public int ExpectedParticipantCount { get; set; }

    public IReadOnlyList<int> StepDurationsMs { get; set; } = Array.Empty<int>();
}
