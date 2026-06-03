using System.ComponentModel.DataAnnotations;

namespace Syntwin.Application.RobotPrograms.Dtos;

public sealed class UpdateRobotProgramRequest
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Status { get; set; }

    [Required]
    [MinLength(1)]
    public List<RobotProgramStepRequest> Steps { get; set; } = new();
}