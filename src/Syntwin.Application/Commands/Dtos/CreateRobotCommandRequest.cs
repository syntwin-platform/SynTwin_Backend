using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Syntwin.Application.Commands.Dtos;

public sealed class CreateRobotCommandRequest
{
    [Required]
    public string CommandType { get; set; } = string.Empty;

    public JsonElement? Payload { get; set; }
}