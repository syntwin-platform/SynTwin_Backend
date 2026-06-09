namespace Syntwin.Application.Realtime.Dtos;

public sealed class ProgramUpdatedEvent
{
    public Guid ProgramId { get; set; }

    public Guid RobotId { get; set; }

    public string ProgramName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string ChangeType { get; set; } = string.Empty;

    public DateTimeOffset ChangedAt { get; set; }
}