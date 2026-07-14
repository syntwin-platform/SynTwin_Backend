using System.ComponentModel.DataAnnotations;
using Syntwin.Application.RobotSafety.Policies;

namespace Syntwin.Application.RobotSafety.Dtos;

public sealed class UpsertSafetyPolicyRequest
{
    [Required]
    public RobotSafetyPolicyDefinition Policy { get; set; } = new();
}