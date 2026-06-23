namespace Syntwin.Application.LuaParsing.Models;

public sealed class LuaParseDiagnostic
{
    public int Line { get; set; }

    public string Severity { get; set; } = "Error";

    public string Message { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;
}