using System.Text.Json;
using Syntwin.Application.AuditLogs.Interfaces;
using Syntwin.Application.Companies.Interfaces;
using Syntwin.Application.RobotSafety.Dtos;
using Syntwin.Application.RobotSafety.Interfaces;
using Syntwin.Application.RobotSafety.Policies;
using Syntwin.Application.Robots.Interfaces;
using Syntwin.Domain.Entities;
using Syntwin.Domain.Enums;
using DomainRobotSafetyPolicy = Syntwin.Domain.Entities.RobotSafetyPolicy;

namespace Syntwin.Application.RobotSafety.Services;

public sealed class RobotSafetyPolicyService : IRobotSafetyPolicyService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ICompanyRepository _companyRepository;
    private readonly IRobotRepository _robotRepository;
    private readonly IRobotSafetyPolicyRepository _policyRepository;
    private readonly IRobotSafetyDefaultPolicyFactory _defaultPolicyFactory;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IRobotAccessService _robotAccessService;

    public RobotSafetyPolicyService(
        ICompanyRepository companyRepository,
        IRobotRepository robotRepository,
        IRobotSafetyPolicyRepository policyRepository,
        IRobotSafetyDefaultPolicyFactory defaultPolicyFactory,
        IAuditLogRepository auditLogRepository,
        IRobotAccessService robotAccessService)
    {
        _companyRepository = companyRepository;
        _robotRepository = robotRepository;
        _policyRepository = policyRepository;
        _defaultPolicyFactory = defaultPolicyFactory;
        _auditLogRepository = auditLogRepository;
        _robotAccessService = robotAccessService;
    }

    public async Task<SafetyPolicyResponse?> GetCompanyPolicyAsync(
        Guid userId,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var role = await GetCompanyRoleOrNullAsync(userId, companyId, cancellationToken);

        if (!role.HasValue)
        {
            return null;
        }

        var canManage = role == CompanyMemberRole.Owner;
        var company = await _companyRepository.GetByIdAsync(companyId, cancellationToken);

        if (company is null || company.Status != CompanyStatus.Active)
        {
            return null;
        }

        var companyPolicy = await _policyRepository.GetActiveCompanyPolicyAsync(
            companyId,
            cancellationToken);

        if (companyPolicy is not null &&
            TryReadPolicy(companyPolicy, out var policy))
        {
            return ToResponse(
                SafetyPolicySource.Company,
                companyId,
                robotId: null,
                companyPolicy,
                policy,
                canManage);
        }

        return ToDefaultResponse(
            companyId,
            robotId: null,
            company.CreatedByUser?.CreatedCompanies.FirstOrDefault()?.Name ?? "Fairino FR5",
            canManage);
    }

    public async Task<SafetyPolicyResponse?> UpsertCompanyPolicyAsync(
        Guid userId,
        Guid companyId,
        UpsertSafetyPolicyRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        await EnsureOwnerAsync(userId, companyId, cancellationToken);

        var company = await _companyRepository.GetByIdAsync(companyId, cancellationToken);

        if (company is null || company.Status != CompanyStatus.Active)
        {
            return null;
        }

        ValidatePolicyDefinition(request.Policy);

        var now = DateTimeOffset.UtcNow;
        var policyJson = JsonSerializer.Serialize(request.Policy, JsonOptions);

        var existing = await _policyRepository.GetCompanyPolicyForUpdateAsync(
            companyId,
            cancellationToken);

        if (existing is null)
        {
            existing = new DomainRobotSafetyPolicy
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                RobotId = null,
                Scope = RobotSafetyPolicyScope.Company,
                Name = request.Policy.Name.Trim(),
                RobotModel = NormalizeRobotModel(request.Policy.RobotModel),
                PolicyJson = policyJson,
                IsActive = true,
                CreatedByUserId = userId,
                CreatedAt = now
            };

            await _policyRepository.AddAsync(existing, cancellationToken);
        }
        else
        {
            existing.Name = request.Policy.Name.Trim();
            existing.RobotModel = NormalizeRobotModel(request.Policy.RobotModel);
            existing.PolicyJson = policyJson;
            existing.UpdatedByUserId = userId;
            existing.UpdatedAt = now;
        }

        await AddPolicyAuditAsync(
            userId,
            companyId,
            robotId: null,
            "COMPANY_SAFETY_POLICY_UPDATED",
            "Company safety policy was updated.",
            request.Policy,
            ipAddress,
            cancellationToken);

        await _policyRepository.SaveChangesAsync(cancellationToken);

        return ToResponse(
            SafetyPolicySource.Company,
            companyId,
            robotId: null,
            existing,
            request.Policy,
            canManage: true);
    }

    public async Task<SafetyPolicyResponse?> GetRobotPolicyAsync(
        Guid userId,
        Guid robotId,
        CancellationToken cancellationToken = default)
    {
        var robot = await _robotRepository.GetByIdAsync(robotId, cancellationToken);

        if (robot is null)
        {
            return null;
        }

        var role = await GetCompanyRoleOrNullAsync(userId, robot.CompanyId, cancellationToken);

        if (!role.HasValue)
        {
            return null;
        }

        var canManage = role == CompanyMemberRole.Owner;
        var robotPolicy = await _policyRepository.GetActiveRobotPolicyAsync(
            robotId,
            cancellationToken);

        if (robotPolicy is not null &&
            TryReadPolicy(robotPolicy, out var robotPolicyDefinition))
        {
            return ToResponse(
                SafetyPolicySource.Robot,
                robot.CompanyId,
                robotId,
                robotPolicy,
                robotPolicyDefinition,
                canManage);
        }

        var companyPolicy = await _policyRepository.GetActiveCompanyPolicyAsync(
            robot.CompanyId,
            cancellationToken);

        if (companyPolicy is not null &&
            TryReadPolicy(companyPolicy, out var companyPolicyDefinition))
        {
            return ToResponse(
                SafetyPolicySource.Company,
                robot.CompanyId,
                robotId,
                companyPolicy,
                companyPolicyDefinition,
                canManage);
        }

        return ToDefaultResponse(
            robot.CompanyId,
            robotId,
            robot.Model,
            canManage);
    }

    public async Task<SafetyPolicyResponse?> UpsertRobotPolicyAsync(
        Guid userId,
        Guid robotId,
        UpsertSafetyPolicyRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var robot = await _robotRepository.GetByIdAsync(robotId, cancellationToken);

        if (robot is null)
        {
            return null;
        }

        await EnsureOwnerAsync(userId, robot.CompanyId, cancellationToken);

        ValidatePolicyDefinition(request.Policy);

        var now = DateTimeOffset.UtcNow;
        var policyJson = JsonSerializer.Serialize(request.Policy, JsonOptions);

        var existing = await _policyRepository.GetRobotPolicyForUpdateAsync(
            robotId,
            cancellationToken);

        if (existing is null)
        {
            existing = new DomainRobotSafetyPolicy
            {
                Id = Guid.NewGuid(),
                CompanyId = robot.CompanyId,
                RobotId = robotId,
                Scope = RobotSafetyPolicyScope.Robot,
                Name = request.Policy.Name.Trim(),
                RobotModel = NormalizeRobotModel(request.Policy.RobotModel),
                PolicyJson = policyJson,
                IsActive = true,
                CreatedByUserId = userId,
                CreatedAt = now
            };

            await _policyRepository.AddAsync(existing, cancellationToken);
        }
        else
        {
            existing.Name = request.Policy.Name.Trim();
            existing.RobotModel = NormalizeRobotModel(request.Policy.RobotModel);
            existing.PolicyJson = policyJson;
            existing.UpdatedByUserId = userId;
            existing.UpdatedAt = now;
        }

        await AddPolicyAuditAsync(
            userId,
            robot.CompanyId,
            robotId,
            "ROBOT_SAFETY_POLICY_UPDATED",
            "Robot safety policy was updated.",
            request.Policy,
            ipAddress,
            cancellationToken);

        await _policyRepository.SaveChangesAsync(cancellationToken);

        return ToResponse(
            SafetyPolicySource.Robot,
            robot.CompanyId,
            robotId,
            existing,
            request.Policy,
            canManage: true);
    }

    public async Task<bool> DeleteRobotPolicyAsync(
        Guid userId,
        Guid robotId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var robot = await _robotRepository.GetByIdAsync(robotId, cancellationToken);

        if (robot is null)
        {
            return false;
        }

        await EnsureOwnerAsync(userId, robot.CompanyId, cancellationToken);

        var existing = await _policyRepository.GetRobotPolicyForUpdateAsync(
            robotId,
            cancellationToken);

        if (existing is null)
        {
            return true;
        }

        existing.IsActive = false;
        existing.UpdatedByUserId = userId;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        await AddPolicyAuditAsync(
            userId,
            robot.CompanyId,
            robotId,
            "ROBOT_SAFETY_POLICY_DELETED",
            "Robot safety policy override was deleted.",
            policy: null,
            ipAddress,
            cancellationToken);

        await _policyRepository.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task<CompanyMemberRole?> GetCompanyRoleOrNullAsync(
        Guid userId,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        return await _robotAccessService.GetCompanyRoleAsync(
            userId,
            companyId,
            cancellationToken);
    }

    private async Task EnsureOwnerAsync(
        Guid userId,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var role = await GetCompanyRoleOrNullAsync(
            userId,
            companyId,
            cancellationToken);

        if (role != CompanyMemberRole.Owner)
        {
            throw new UnauthorizedAccessException(
                "Only the company owner can manage robot safety policies.");
        }
    }

    private static bool TryReadPolicy(
        DomainRobotSafetyPolicy? storedPolicy,
        out RobotSafetyPolicyDefinition policy)
    {
        policy = new RobotSafetyPolicyDefinition();

        if (storedPolicy is null ||
            string.IsNullOrWhiteSpace(storedPolicy.PolicyJson))
        {
            return false;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<RobotSafetyPolicyDefinition>(
                storedPolicy.PolicyJson,
                JsonOptions);

            if (parsed is null)
            {
                return false;
            }

            policy = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private SafetyPolicyResponse ToDefaultResponse(
        Guid companyId,
        Guid? robotId,
        string? robotModel,
        bool canManage)
    {
        return new SafetyPolicyResponse
        {
            Source = SafetyPolicySource.Default,
            CompanyId = companyId,
            RobotId = robotId,
            CanManage = canManage,
            Policy = _defaultPolicyFactory.CreateDefaultPolicy(robotModel)
        };
    }

    private static SafetyPolicyResponse ToResponse(
        SafetyPolicySource source,
        Guid companyId,
        Guid? robotId,
        DomainRobotSafetyPolicy storedPolicy,
        RobotSafetyPolicyDefinition policy,
        bool canManage)
    {
        return new SafetyPolicyResponse
        {
            Source = source,
            PolicyId = storedPolicy.Id,
            CompanyId = companyId,
            RobotId = robotId,
            CanManage = canManage,
            Policy = policy,
            UpdatedAt = storedPolicy.UpdatedAt ?? storedPolicy.CreatedAt
        };
    }

    private static void ValidatePolicyDefinition(
        RobotSafetyPolicyDefinition policy)
    {
        if (string.IsNullOrWhiteSpace(policy.Name))
        {
            throw new InvalidOperationException("Safety policy name is required.");
        }

        if (policy.JointLimits.Count != 6)
        {
            throw new InvalidOperationException("Safety policy must include exactly 6 joint limits.");
        }

        var duplicateJoint = policy.JointLimits
            .GroupBy(limit => limit.Joint)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateJoint is not null)
        {
            throw new InvalidOperationException($"Duplicate joint limit: joint {duplicateJoint.Key}.");
        }

        foreach (var limit in policy.JointLimits)
        {
            if (limit.Joint is < 1 or > 6)
            {
                throw new InvalidOperationException("Joint number must be between 1 and 6.");
            }

            if (limit.MinDeg >= limit.MaxDeg)
            {
                throw new InvalidOperationException($"Joint {limit.Joint} min angle must be less than max angle.");
            }
        }

        if (policy.TcpWorkspace.MinX >= policy.TcpWorkspace.MaxX ||
            policy.TcpWorkspace.MinY >= policy.TcpWorkspace.MaxY ||
            policy.TcpWorkspace.MinZ >= policy.TcpWorkspace.MaxZ ||
            policy.TcpWorkspace.MinRotationDeg >= policy.TcpWorkspace.MaxRotationDeg)
        {
            throw new InvalidOperationException("TCP workspace min values must be less than max values.");
        }

        if (policy.MinSpeedPercent < 1 ||
            policy.MaxSpeedPercent > 100 ||
            policy.MinSpeedPercent > policy.MaxSpeedPercent)
        {
            throw new InvalidOperationException("Speed percent range must be within 1..100.");
        }

        if (policy.MinAccelerationPercent < 1 ||
            policy.MaxAccelerationPercent > 100 ||
            policy.MinAccelerationPercent > policy.MaxAccelerationPercent)
        {
            throw new InvalidOperationException("Acceleration percent range must be within 1..100.");
        }

        if (policy.MaxJointDeltaDegPerStep <= 0 ||
            policy.MaxFirstStepJointDeltaDeg <= 0)
        {
            throw new InvalidOperationException("Joint delta limits must be greater than 0.");
        }
    }

    private static string NormalizeRobotModel(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "Fairino FR5"
            : value.Trim();
    }

    private async Task AddPolicyAuditAsync(
        Guid userId,
        Guid companyId,
        Guid? robotId,
        string action,
        string message,
        RobotSafetyPolicyDefinition? policy,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        await _auditLogRepository.AddAsync(new AuditLog
        {
            UserId = userId,
            RobotId = robotId,
            Action = action,
            IpAddress = NormalizeNullable(ipAddress),
            Message = message,
            RawPayloadJson = JsonSerializer.Serialize(new
            {
                companyId,
                robotId,
                policy
            }, JsonOptions),
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
