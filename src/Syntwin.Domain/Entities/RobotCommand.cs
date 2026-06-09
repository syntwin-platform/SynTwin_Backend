using Syntwin.Domain.Enums;

namespace Syntwin.Domain.Entities;

public sealed class RobotCommand
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RobotId { get; set; }

    public Guid UserId { get; set; }

    public RobotCommandType CommandType { get; set; }

    public string? PayloadJson { get; set; }

    public CommandStatus Status { get; set; } = CommandStatus.Pending;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? SentAt { get; set; }
    public int DeliveryAttemptCount { get; set; }

    public DateTimeOffset? LastDeliveryAttemptAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public DateTimeOffset? TimeoutAt { get; set; }

    public string? FailureReason { get; set; }

    public Robot? Robot { get; set; }

    public User? User { get; set; }

    public CommandResult? Result { get; set; }
}
