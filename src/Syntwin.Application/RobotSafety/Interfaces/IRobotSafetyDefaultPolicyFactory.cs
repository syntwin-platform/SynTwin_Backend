using Syntwin.Application.RobotSafety.Policies;

namespace Syntwin.Application.RobotSafety.Interfaces;

public interface IRobotSafetyDefaultPolicyFactory
{
    RobotSafetyPolicyDefinition CreateDefaultPolicy(string? robotModel);
}