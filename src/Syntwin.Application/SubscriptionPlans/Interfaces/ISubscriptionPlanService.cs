using Syntwin.Application.SubscriptionPlans.Dtos;

namespace Syntwin.Application.SubscriptionPlans.Interfaces;

public interface ISubscriptionPlanService
{
    Task<IReadOnlyList<SubscriptionPlanResponse>> GetActivePlansAsync(
        CancellationToken cancellationToken = default);
}
