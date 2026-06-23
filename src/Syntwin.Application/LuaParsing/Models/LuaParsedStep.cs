using System.Text.Json;

namespace Syntwin.Application.LuaParsing.Models;

public sealed class LuaParsedStep
{
    public int OrderIndex { get; set; }

    public string StepType { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public JsonElement Payload { get; set; }

    public string? Raw { get; set; }

    public string? PointRef { get; set; }
}