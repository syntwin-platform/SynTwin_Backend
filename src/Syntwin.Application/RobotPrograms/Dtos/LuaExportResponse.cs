namespace Syntwin.Application.RobotPrograms.Dtos;

public sealed class LuaExportResponse
{
    public Guid RobotId { get; set; }

    public Guid ProgramId { get; set; }

    public string ProgramName { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string LuaContent { get; set; } = string.Empty;

    public DateTimeOffset ExportedAt { get; set; }
}