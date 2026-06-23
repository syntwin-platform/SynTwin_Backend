using System.ComponentModel.DataAnnotations;

namespace Syntwin.Application.LuaParsing.Dtos;

public sealed class LuaParseRequest
{
    [Required]
    public string LuaContent { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? FileName { get; set; }
}