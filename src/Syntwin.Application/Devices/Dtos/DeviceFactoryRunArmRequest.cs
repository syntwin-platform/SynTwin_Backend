using System.ComponentModel.DataAnnotations;

namespace Syntwin.Application.Devices.Dtos;

public sealed class DeviceFactoryRunArmRequest
{
    [Required]
    public Guid FactoryRunId { get; set; }

    [Required]
    public Guid TargetId { get; set; }

    [Required]
    public Guid CommandId { get; set; }

    [Required]
    public Guid RobotId { get; set; }

    public DateTimeOffset? ReceivedAtUtc { get; set; }

    public DateTimeOffset? ArmedAtUtc { get; set; }

    public IReadOnlyList<int> EstimatedStepDurationsMs { get; set; } = Array.Empty<int>();
}
