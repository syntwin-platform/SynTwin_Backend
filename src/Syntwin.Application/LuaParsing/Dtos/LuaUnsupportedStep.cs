namespace Syntwin.Application.LuaParsing.Dtos;

public sealed class LuaUnsupportedStep
{
    public int OrderIndex { get; set; }

    public string StepType { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;
}
