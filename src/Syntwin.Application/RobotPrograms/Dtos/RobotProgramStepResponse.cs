using System.Text.Json;

namespace Syntwin.Application.RobotPrograms.Dtos;

public sealed class RobotProgramStepResponse
{
    public Guid Id { get; set; }

    public int OrderIndex { get; set; }

    public string StepType { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public JsonElement Payload { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}