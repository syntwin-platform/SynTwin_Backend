using System.ComponentModel.DataAnnotations;

namespace Syntwin.Application.FactoryRuns.Dtos;

public sealed class CreateFactoryRunProgramRequest
{
    [Required]
    [MaxLength(100)]
    public string Key { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string ProgramName { get; set; } = string.Empty;

    [Required]
    [MaxLength(260)]
    public string LuaFileName { get; set; } = string.Empty;

    [Required]
    public string LuaContent { get; set; } = string.Empty;
}
