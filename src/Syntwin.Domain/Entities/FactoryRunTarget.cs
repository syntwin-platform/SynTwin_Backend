using Syntwin.Domain.Enums;

namespace Syntwin.Domain.Entities;

public sealed class FactoryRunTarget
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid FactoryRunId { get; set; }

    public Guid RobotId { get; set; }

    public Guid? ProgramId { get; set; }
    public Guid? PrepareCommandId { get; set; }
    public Guid? CommandId { get; set; }

    public Guid? RuntimeSessionId { get; set; }

    public FactoryRunTargetStatus Status { get; set; } = FactoryRunTargetStatus.Pending;
    public FactoryRunTargetTerminationReason? TerminationReason { get; set; }

    public string? ReadinessError { get; set; }

    public DateTimeOffset? PrepareStartedAtUtc { get; set; }

    public DateTimeOffset? PreparedAtUtc { get; set; }

    public DateTimeOffset? ReadyAtUtc { get; set; }

    public DateTimeOffset? CommandReceivedAtUtc { get; set; }

    public DateTimeOffset? ArmedAtUtc { get; set; }

    public string? EstimatedStepDurationsJson { get; set; }

    public DateTimeOffset? StartedAtUtc { get; set; }

    public DateTimeOffset? ActualStartedAtUtc { get; set; }

    public int? StartLateByMs { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public string? FailureReason { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public FactoryRun? FactoryRun { get; set; }

    public Robot? Robot { get; set; }

    public RobotProgram? Program { get; set; }

    public RobotCommand? PrepareCommand { get; set; }

    public RobotCommand? Command { get; set; }
}