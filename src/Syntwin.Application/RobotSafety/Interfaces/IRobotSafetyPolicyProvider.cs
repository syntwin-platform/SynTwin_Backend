using Syntwin.Application.RobotSafety.Policies;

namespace Syntwin.Application.RobotSafety.Interfaces;

public interface IRobotSafetyPolicyProvider
{
    Task<RobotSafetyPolicyDefinition> GetPolicyAsync(
        Guid robotId,
        Guid companyId,
        string? robotModel,
        CancellationToken cancellationToken = default);
}