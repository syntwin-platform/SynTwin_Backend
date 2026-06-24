using Syntwin.Application.RobotSafety.Dtos;

namespace Syntwin.Application.RobotSafety.Exceptions;

public sealed class RobotSafetyValidationException : InvalidOperationException
{
    public RobotSafetyValidationException(SafetyValidationResult result)
        : base(CreateMessage(result))
    {
        Result = result;
    }

    public SafetyValidationResult Result { get; }

    private static string CreateMessage(SafetyValidationResult result)
    {
        var firstBlocker = result.Diagnostics.FirstOrDefault();

        return firstBlocker is null
            ? "Program failed safety validation."
            : $"Program failed safety validation: {firstBlocker.Message}";
    }
}