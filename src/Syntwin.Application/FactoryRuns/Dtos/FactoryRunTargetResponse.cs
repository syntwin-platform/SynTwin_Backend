namespace Syntwin.Application.FactoryRuns.Dtos;

public sealed class FactoryRunTargetResponse
{
    public Guid Id { get; set; }

    public Guid FactoryRunId { get; set; }

    public Guid RobotId { get; set; }

    public Guid? FactoryRunProgramId { get; set; }

    public Guid? ProgramId { get; set; }
    public Guid? CommandId { get; set; }
    public Guid? PrepareCommandId { get; set; }

    public Guid? RuntimeSessionId { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? TerminationReason { get; set; }

    public string? ReadinessError { get; set; }

    public DateTimeOffset? PrepareStartedAtUtc { get; set; }

    public DateTimeOffset? PreparedAtUtc { get; set; }

    public DateTimeOffset? ReadyAtUtc { get; set; }

    public DateTimeOffset? CommandReceivedAtUtc { get; set; }

    public DateTimeOffset? ArmedAtUtc { get; set; }
    public IReadOnlyList<int> EstimatedStepDurationsMs { get; set; } = Array.Empty<int>();

    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? ActualStartedAtUtc { get; set; }

    public int? StartLateByMs { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public string? FailureReason { get; set; }
}
