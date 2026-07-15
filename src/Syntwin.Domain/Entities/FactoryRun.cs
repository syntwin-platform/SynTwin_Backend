using Syntwin.Domain.Enums;

namespace Syntwin.Domain.Entities;

public sealed class FactoryRun
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompanyId { get; set; }

    public Guid CreatedByUserId { get; set; }

    public FactoryRunStatus Status { get; set; } = FactoryRunStatus.Created;

    public FactoryCoordinationMode CoordinationMode { get; set; } =
        FactoryCoordinationMode.Synchronized;

    public FactoryFailurePolicy FailurePolicy { get; set; } =
        FactoryFailurePolicy.IsolateTarget;

    public string ProgramName { get; set; } = string.Empty;

    public string LuaFileName { get; set; } = string.Empty;

    public string LuaContent { get; set; } = string.Empty;

    public string LuaContentHash { get; set; } = string.Empty;

    public int TargetCount { get; set; }

    public DateTimeOffset? ScheduledStartAtUtc { get; set; }

    public string? StepDurationsJson { get; set; }

    public DateTimeOffset? PreparedAtUtc { get; set; }

    public DateTimeOffset? StartedAtUtc { get; set; }
    public int? ActualStartSkewMs { get; set; }


    public DateTimeOffset? CompletedAtUtc { get; set; }

    public DateTimeOffset? CancelledAtUtc { get; set; }

    public string? FailureReason { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public Company? Company { get; set; }

    public User? CreatedByUser { get; set; }

    public ICollection<FactoryRunProgram> Programs { get; set; } =
        new List<FactoryRunProgram>();

    public ICollection<FactoryRunTarget> Targets { get; set; } = new List<FactoryRunTarget>();
}
