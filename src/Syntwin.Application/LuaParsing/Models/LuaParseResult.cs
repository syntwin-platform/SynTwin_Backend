namespace Syntwin.Application.LuaParsing.Models;

public sealed class LuaParseResult
{
    public LuaProgramMetadata Metadata { get; set; } = new();

    public Dictionary<string, object?> Variables { get; set; } = new();

    public Dictionary<string, LuaRobotPoint> Points { get; set; } = new();

    public List<LuaParsedStep> Steps { get; set; } = new();

    public List<LuaParseDiagnostic> Diagnostics { get; set; } = new();
}