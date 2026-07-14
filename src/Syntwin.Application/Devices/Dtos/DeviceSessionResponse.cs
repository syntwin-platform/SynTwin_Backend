namespace Syntwin.Application.Devices.Dtos;

public sealed class DeviceSessionResponse
{
    public Guid RobotId { get; set; }

    public Guid? RuntimeSessionId { get; set; }

    public string AccessToken { get; set; } = string.Empty;

    public int ExpiresInSeconds { get; set; }
}