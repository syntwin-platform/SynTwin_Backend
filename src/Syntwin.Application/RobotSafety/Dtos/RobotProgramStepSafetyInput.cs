using System.Text.Json;

namespace Syntwin.Application.RobotSafety.Dtos;

public sealed class RobotProgramStepSafetyInput
{
    public int OrderIndex { get; set; }

    public string StepType { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public JsonElement Payload { get; set; }
}