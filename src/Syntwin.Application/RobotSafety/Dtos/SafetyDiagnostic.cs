using Syntwin.Application.RobotSafety.Enums;

namespace Syntwin.Application.RobotSafety.Dtos;

public sealed class SafetyDiagnostic
{
    public SafetySeverity Severity { get; set; }

    public string Code { get; set; } = string.Empty;

    public int? StepOrderIndex { get; set; }

    public string? StepLabel { get; set; }

    public string? Field { get; set; }

    public string Message { get; set; } = string.Empty;
}