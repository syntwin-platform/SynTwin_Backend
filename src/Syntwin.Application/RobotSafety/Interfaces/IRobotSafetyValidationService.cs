using Syntwin.Application.RobotSafety.Dtos;

namespace Syntwin.Application.RobotSafety.Interfaces;

public interface IRobotSafetyValidationService
{
    Task<SafetyValidationResult> ValidateProgramAsync(
        RobotSafetyValidationRequest request,
        CancellationToken cancellationToken = default);
}