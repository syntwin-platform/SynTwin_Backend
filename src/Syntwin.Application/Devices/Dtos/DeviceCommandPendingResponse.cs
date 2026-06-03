using System.Text.Json;

namespace Syntwin.Application.Devices.Dtos;

public sealed class DeviceCommandPendingResponse
{
    public Guid CommandId { get; set; }
    public Guid RobotId { get; set; }
    public string CommandType { get; set; } = string.Empty;
    public JsonElement? Payload { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}