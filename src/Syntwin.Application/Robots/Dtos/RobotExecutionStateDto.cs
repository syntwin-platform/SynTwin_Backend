using System.ComponentModel.DataAnnotations;

namespace Syntwin.Application.Robots.Dtos;

public sealed class RobotExecutionStateDto
{
    public Guid? CurrentCommandId { get; set; }

    [MaxLength(30)]
    public string? State { get; set; }

    [Range(0, int.MaxValue)]
    public int? CurrentStepIndex { get; set; }

    [Range(0, int.MaxValue)]
    public int? TotalSteps { get; set; }

    [Range(0, 100)]
    public double? ProgressPercent { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    [MaxLength(500)]
    public string? LastError { get; set; }
}
