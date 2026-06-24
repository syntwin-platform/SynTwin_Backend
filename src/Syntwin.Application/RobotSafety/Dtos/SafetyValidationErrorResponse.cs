namespace Syntwin.Application.RobotSafety.Dtos;

public sealed class SafetyValidationErrorResponse
{
    public string Message { get; set; } = string.Empty;

    public IReadOnlyList<SafetyDiagnostic> Diagnostics { get; set; } = Array.Empty<SafetyDiagnostic>();
}