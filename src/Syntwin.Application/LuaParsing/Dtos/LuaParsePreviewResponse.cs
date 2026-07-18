using Syntwin.Application.LuaParsing.Models;
using Syntwin.Application.RobotPrograms.Dtos;

namespace Syntwin.Application.LuaParsing.Dtos;

public sealed class LuaParsePreviewResponse
{
    public LuaProgramMetadata Metadata { get; set; } = new();

    public Dictionary<string, object?> Variables { get; set; } = new();

    public Dictionary<string, LuaRobotPoint> Points { get; set; } = new();

    public List<LuaParsedStep> ParsedSteps { get; set; } = new();

    public List<LuaParseDiagnostic> Diagnostics { get; set; } = new();

    public CreateRobotProgramRequest? CreateProgramRequest { get; set; }

    public bool ExecutionReady { get; set; }

    public string? CompiledProgramHash { get; set; }

    public List<LuaUnsupportedStep> UnsupportedSteps { get; set; } = new();
}
