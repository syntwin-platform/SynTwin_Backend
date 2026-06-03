using Syntwin.Application.SubscriptionPlans.Dtos;
using Syntwin.Application.SubscriptionPlans.Interfaces;
using Syntwin.Application.Users.Interfaces;
using Syntwin.Domain.Entities;

namespace Syntwin.Application.SubscriptionPlans.Services;

public sealed class SubscriptionPlanService : ISubscriptionPlanService
{
    private readonly IUserRepository _userRepository;

    public SubscriptionPlanService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<IReadOnlyList<SubscriptionPlanResponse>> GetActivePlansAsync(
        CancellationToken cancellationToken = default)
    {
        var plans = await _userRepository.GetActiveSubscriptionPlansAsync(cancellationToken);

        return plans
            .Select(ToResponse)
            .ToArray();
    }

    private static SubscriptionPlanResponse ToResponse(SubscriptionPlan plan)
    {
        return new SubscriptionPlanResponse
        {
            Id = plan.Id,
            Code = plan.Code.ToString(),
            Name = plan.Name,
            MonthlyPrice = plan.MonthlyPrice,
            MaxRobots = plan.MaxRobots,
            CanView3D = plan.CanView3D,
            CanSendCommand = plan.CanSendCommand,
            AuditRetentionDays = plan.AuditRetentionDays
        };
    }
}
