namespace Syntwin.Domain.Entities;

public sealed class FactoryRunProgram
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid FactoryRunId { get; set; }

    public string ProgramKey { get; set; } = string.Empty;

    public string ProgramName { get; set; } = string.Empty;

    public string LuaFileName { get; set; } = string.Empty;

    public string LuaContent { get; set; } = string.Empty;

    public string LuaContentHash { get; set; } = string.Empty;

    public string? SyncPlanHash { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public FactoryRun? FactoryRun { get; set; }

    public ICollection<FactoryRunTarget> Targets { get; set; } =
        new List<FactoryRunTarget>();
}
