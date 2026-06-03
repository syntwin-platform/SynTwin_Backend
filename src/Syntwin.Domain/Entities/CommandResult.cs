namespace Syntwin.Domain.Entities;

public sealed class CommandResult
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CommandId { get; set; }

    public Guid RobotId { get; set; }

    public bool Success { get; set; }

    public string? Message { get; set; }

    public string? RawPayloadJson { get; set; }

    public DateTimeOffset CompletedAt { get; set; } = DateTimeOffset.UtcNow;

    public RobotCommand? Command { get; set; }

    public Robot? Robot { get; set; }
}
