namespace Syntwin.Application.Robots.Dtos;

public sealed class CreateRobotResponse
{
    public RobotResponse Robot { get; set; } = new();

    public string DeviceSecret { get; set; } = string.Empty;
}