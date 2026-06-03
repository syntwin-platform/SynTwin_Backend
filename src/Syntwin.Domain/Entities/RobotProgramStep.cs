using Syntwin.Domain.Enums;

namespace Syntwin.Domain.Entities;

public sealed class RobotProgramStep
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProgramId { get; set; }

    public int OrderIndex { get; set; }

    public RobotProgramStepType StepType { get; set; }

    public string Label { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = "{}";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public RobotProgram? Program { get; set; }
}