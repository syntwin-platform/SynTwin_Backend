namespace Syntwin.Application.Robots.Dtos;

public sealed class ResetRobotDeviceSecretResponse
{
    public Guid RobotId { get; set; }

    public string DeviceSecret { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }
}