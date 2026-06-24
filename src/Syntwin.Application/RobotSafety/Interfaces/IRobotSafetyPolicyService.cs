using Syntwin.Application.RobotSafety.Dtos;

namespace Syntwin.Application.RobotSafety.Interfaces;

public interface IRobotSafetyPolicyService
{
    Task<SafetyPolicyResponse?> GetCompanyPolicyAsync(
        Guid userId,
        Guid companyId,
        CancellationToken cancellationToken = default);

    Task<SafetyPolicyResponse?> UpsertCompanyPolicyAsync(
        Guid userId,
        Guid companyId,
        UpsertSafetyPolicyRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<SafetyPolicyResponse?> GetRobotPolicyAsync(
        Guid userId,
        Guid robotId,
        CancellationToken cancellationToken = default);

    Task<SafetyPolicyResponse?> UpsertRobotPolicyAsync(
        Guid userId,
        Guid robotId,
        UpsertSafetyPolicyRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteRobotPolicyAsync(
        Guid userId,
        Guid robotId,
        string? ipAddress,
        CancellationToken cancellationToken = default);
}