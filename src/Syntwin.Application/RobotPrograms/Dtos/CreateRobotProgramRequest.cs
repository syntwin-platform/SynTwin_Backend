using System.ComponentModel.DataAnnotations;

namespace Syntwin.Application.RobotPrograms.Dtos;

public sealed class CreateRobotProgramRequest
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Status { get; set; }

    [MaxLength(50)]
    public string? Source { get; set; }

    [Required]
    [MinLength(1)]
    public List<RobotProgramStepRequest> Steps { get; set; } = new();
}