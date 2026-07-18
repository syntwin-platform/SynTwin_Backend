namespace Syntwin.Application.FactoryRuns.Dtos;

public sealed class FactoryRunProgramResponse
{
    public Guid Id { get; set; }

    public Guid FactoryRunId { get; set; }

    public string ProgramKey { get; set; } = string.Empty;

    public string ProgramName { get; set; } = string.Empty;

    public string LuaFileName { get; set; } = string.Empty;

    public string LuaContentHash { get; set; } = string.Empty;

    public string? CompiledProgramHash { get; set; }

    public string? SyncPlanHash { get; set; }
}
