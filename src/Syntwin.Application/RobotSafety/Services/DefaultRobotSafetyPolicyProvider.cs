using System.Text.Json;
using Syntwin.Application.RobotSafety.Interfaces;
using Syntwin.Application.RobotSafety.Policies;

namespace Syntwin.Application.RobotSafety.Services;

public sealed class DefaultRobotSafetyPolicyProvider : IRobotSafetyPolicyProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IRobotSafetyPolicyRepository _policyRepository;
    private readonly IRobotSafetyDefaultPolicyFactory _defaultPolicyFactory;

    public DefaultRobotSafetyPolicyProvider(
        IRobotSafetyPolicyRepository policyRepository,
        IRobotSafetyDefaultPolicyFactory defaultPolicyFactory)
    {
        _policyRepository = policyRepository;
        _defaultPolicyFactory = defaultPolicyFactory;
    }

    public async Task<RobotSafetyPolicyDefinition> GetPolicyAsync(
        Guid robotId,
        Guid companyId,
        string? robotModel,
        CancellationToken cancellationToken = default)
    {
        var robotPolicy = await _policyRepository.GetActiveRobotPolicyAsync(
            robotId,
            cancellationToken);

        if (TryDeserializePolicy(robotPolicy?.PolicyJson, out var robotPolicyDefinition))
        {
            return robotPolicyDefinition;
        }

        var companyPolicy = await _policyRepository.GetActiveCompanyPolicyAsync(
            companyId,
            cancellationToken);

        if (TryDeserializePolicy(companyPolicy?.PolicyJson, out var companyPolicyDefinition))
        {
            return companyPolicyDefinition;
        }

        return _defaultPolicyFactory.CreateDefaultPolicy(robotModel);
    }

    private static bool TryDeserializePolicy(
        string? policyJson,
        out RobotSafetyPolicyDefinition policy)
    {
        policy = new RobotSafetyPolicyDefinition();

        if (string.IsNullOrWhiteSpace(policyJson))
        {
            return false;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<RobotSafetyPolicyDefinition>(
                policyJson,
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
}