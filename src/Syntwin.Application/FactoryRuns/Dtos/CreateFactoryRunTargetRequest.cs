using System.ComponentModel.DataAnnotations;

namespace Syntwin.Application.FactoryRuns.Dtos;

public sealed class CreateFactoryRunTargetRequest
{
    [Required]
    public Guid RobotId { get; set; }

    [Required]
    [MaxLength(100)]
    public string ProgramKey { get; set; } = string.Empty;
}
