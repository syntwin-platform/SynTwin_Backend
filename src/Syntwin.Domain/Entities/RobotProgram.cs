using Syntwin.Domain.Enums;

namespace Syntwin.Domain.Entities;

public sealed class RobotProgram
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RobotId { get; set; }

    public string Name { get; set; } = string.Empty;

    public RobotProgramStatus Status { get; set; } = RobotProgramStatus.Draft;

    public RobotProgramSource Source { get; set; } = RobotProgramSource.Studio;

    public Guid CreatedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }

    public Robot? Robot { get; set; }

    public User? CreatedByUser { get; set; }

    public ICollection<RobotProgramStep> Steps { get; set; } = new List<RobotProgramStep>();
}