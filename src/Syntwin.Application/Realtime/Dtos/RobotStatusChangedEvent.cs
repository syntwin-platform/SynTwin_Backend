namespace Syntwin.Application.Realtime.Dtos;

public sealed class RobotStatusChangedEvent
{
    public Guid RobotId { get; set; }

    public string Status { get; set; } = string.Empty;

    public bool IsOnline { get; set; }

    public DateTimeOffset ChangedAt { get; set; }

    public DateTimeOffset? LastSeenAt { get; set; }
}