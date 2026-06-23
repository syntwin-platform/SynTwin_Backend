namespace Syntwin.Application.LuaParsing.Models;

public sealed class LuaProgramMetadata
{
    public string ProjectName { get; set; } = "Imported Project";

    public string? RobotModel { get; set; }

    public string? Date { get; set; }

    public string? Note { get; set; }

    public string? Author { get; set; }

    public string? Version { get; set; }
}