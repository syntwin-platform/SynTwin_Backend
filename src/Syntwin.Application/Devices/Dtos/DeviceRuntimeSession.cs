namespace Syntwin.Application.Devices.Dtos;

public sealed class DeviceRuntimeSession
{
    public Guid RobotId { get; set; }

    public string AccessToken { get; set; } = string.Empty;

    public DateTimeOffset IssuedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
}