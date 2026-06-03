using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Syntwin.Application.RobotPrograms.Dtos;

public sealed class RobotProgramStepRequest
{
    [Range(1, int.MaxValue)]
    public int OrderIndex { get; set; }

    [Required]
    [MaxLength(50)]
    public string StepType { get; set; } = string.Empty;

    [Required]
    [MaxLength(150)]
    public string Label { get; set; } = string.Empty;

    [Required]
    public JsonElement Payload { get; set; }
}