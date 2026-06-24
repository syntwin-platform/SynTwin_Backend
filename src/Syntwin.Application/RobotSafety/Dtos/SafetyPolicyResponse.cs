using Syntwin.Application.RobotSafety.Policies;

namespace Syntwin.Application.RobotSafety.Dtos;

public sealed class SafetyPolicyResponse
{
    public SafetyPolicySource Source { get; set; }

    public Guid? PolicyId { get; set; }

    public Guid CompanyId { get; set; }

    public Guid? RobotId { get; set; }

    public bool CanManage { get; set; }

    public RobotSafetyPolicyDefinition Policy { get; set; } = new();

    public DateTimeOffset? UpdatedAt { get; set; }
}