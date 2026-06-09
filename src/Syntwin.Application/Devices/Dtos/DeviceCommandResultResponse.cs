namespace Syntwin.Application.Devices.Dtos;

public sealed class DeviceCommandResultResponse
{
    public Guid CommandId { get; set; }

    public Guid RobotId { get; set; }

    public string CommandStatus { get; set; } = string.Empty;

    public bool Success { get; set; }

    public string? Message { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public bool IsDuplicate { get; set; }
}
