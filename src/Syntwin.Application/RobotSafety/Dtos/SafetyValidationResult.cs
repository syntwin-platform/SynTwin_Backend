using Syntwin.Application.RobotSafety.Enums;

namespace Syntwin.Application.RobotSafety.Dtos;

public sealed class SafetyValidationResult
{
    public List<SafetyDiagnostic> Diagnostics { get; } = new();

    public bool HasBlockers =>
        Diagnostics.Any(diagnostic => diagnostic.Severity == SafetySeverity.Blocker);

    public bool HasWarnings =>
        Diagnostics.Any(diagnostic => diagnostic.Severity == SafetySeverity.Warning);

    public bool IsSafeToRun => !HasBlockers;
}