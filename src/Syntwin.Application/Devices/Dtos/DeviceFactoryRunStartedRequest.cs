using System.ComponentModel.DataAnnotations;

namespace Syntwin.Application.Devices.Dtos;

public sealed class DeviceFactoryRunStartedRequest
{
    [Required]
    public Guid FactoryRunId { get; set; }

    [Required]
    public Guid TargetId { get; set; }

    [Required]
    public Guid CommandId { get; set; }

    [Required]
    public Guid RobotId { get; set; }

    public DateTimeOffset? ActualStartedAtUtc { get; set; }
}