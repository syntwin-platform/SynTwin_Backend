using System.Text.Json;

namespace Syntwin.Application.Commands.Dtos;

public sealed class RobotCommandResponse
{
    public Guid Id { get; set; }
    public Guid RobotId { get; set; }
    public string CommandType { get; set; } = string.Empty;
    public JsonElement? Payload { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}