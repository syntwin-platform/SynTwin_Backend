namespace Syntwin.Domain.Entities;

public sealed class RobotRuntimeSession
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RobotId { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }

    public DateTimeOffset? DetectedOfflineAt { get; set; }

    public long? DurationSeconds { get; set; }

    public string? EndReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }

    public Robot Robot { get; set; } = null!;
}