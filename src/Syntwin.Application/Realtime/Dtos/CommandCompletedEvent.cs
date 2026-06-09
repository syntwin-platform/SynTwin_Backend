namespace Syntwin.Application.Realtime.Dtos;

public sealed class CommandCompletedEvent
{
    public Guid CommandId { get; set; }

    public Guid RobotId { get; set; }

    public string CommandType { get; set; } = string.Empty;

    public string CommandStatus { get; set; } = string.Empty;

    public bool Success { get; set; }

    public string? Message { get; set; }

    public DateTimeOffset CompletedAt { get; set; }
}