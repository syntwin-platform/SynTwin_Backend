using System.Text.Json;

namespace Syntwin.Application.Commands.Dtos;

public sealed class RobotCommandResultResponse
{
    public bool Success { get; set; }

    public string? Message { get; set; }

    public JsonElement? RawPayload { get; set; }

    public DateTimeOffset CompletedAt { get; set; }
}