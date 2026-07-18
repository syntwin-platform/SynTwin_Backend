namespace Syntwin.Application.FactoryRuns.Dtos;

public sealed class FactoryRunResponse
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }

    public Guid CreatedByUserId { get; set; }

    public Guid? ClientRequestId { get; set; }

    public string Status { get; set; } = string.Empty;

    public string CoordinationMode { get; set; } = string.Empty;

    public string FailurePolicy { get; set; } = string.Empty;

    public string ProgramName { get; set; } = string.Empty;

    public string LuaFileName { get; set; } = string.Empty;

    public string LuaContentHash { get; set; } = string.Empty;

    public int TargetCount { get; set; }

    public DateTimeOffset? ScheduledStartAtUtc { get; set; }
    public IReadOnlyList<int> StepDurationsMs { get; set; } = Array.Empty<int>();

    public DateTimeOffset? PreparedAtUtc { get; set; }

    public DateTimeOffset? StartedAtUtc { get; set; }
    public int? ActualStartSkewMs { get; set; }


    public DateTimeOffset? CompletedAtUtc { get; set; }

    public DateTimeOffset? CancelledAtUtc { get; set; }

    public string? FailureReason { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public IReadOnlyList<FactoryRunProgramResponse> Programs { get; set; } = [];

    public IReadOnlyList<FactoryRunTargetResponse> Targets { get; set; } = [];
}
