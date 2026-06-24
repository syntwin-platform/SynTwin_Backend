namespace Syntwin.Application.RobotSafety.Dtos;

public sealed class RobotSafetyValidationRequest
{
    public Guid RobotId { get; set; }

    public Guid CompanyId { get; set; }

    public string RobotModel { get; set; } = string.Empty;

    public IReadOnlyList<double>? CurrentJointAngles { get; set; }

    public IReadOnlyList<RobotProgramStepSafetyInput> Steps { get; set; }
        = Array.Empty<RobotProgramStepSafetyInput>();
}