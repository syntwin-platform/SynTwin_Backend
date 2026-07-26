namespace Syntwin.Application.FactoryRuns.Dtos;

/// <summary>
/// Lightweight runtime projection used by FactoryRun polling.
/// It intentionally excludes Lua source, compiled programs and command payloads.
/// </summary>
public sealed class FactoryRunStatusResponse
{
    public Guid Id { get; set; }

    public string Status { get; set; } = string.Empty;

    public string CoordinationMode { get; set; } = string.Empty;

    public string FailurePolicy { get; set; } = string.Empty;

    public int TargetCount { get; set; }

    public DateTimeOffset? ScheduledStartAtUtc { get; set; }

    public DateTimeOffset? PreparedAtUtc { get; set; }

    public DateTimeOffset? StartedAtUtc { get; set; }

    public int? ActualStartSkewMs { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public DateTimeOffset? CancelledAtUtc { get; set; }

    public string? FailureReason { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public IReadOnlyList<FactoryRunTargetResponse> Targets { get; set; } = [];
}
