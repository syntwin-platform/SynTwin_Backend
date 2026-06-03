using Microsoft.AspNetCore.Mvc;
using Syntwin.Application.SubscriptionPlans.Dtos;
using Syntwin.Application.SubscriptionPlans.Interfaces;

namespace Syntwin.Api.Controllers;

[ApiController]
[Route("api/subscription-plans")]
public sealed class SubscriptionPlansController : ControllerBase
{
    private readonly ISubscriptionPlanService _subscriptionPlanService;

    public SubscriptionPlansController(ISubscriptionPlanService subscriptionPlanService)
    {
        _subscriptionPlanService = subscriptionPlanService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SubscriptionPlanResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SubscriptionPlanResponse>>> GetActivePlans(
        CancellationToken cancellationToken)
    {
        var plans = await _subscriptionPlanService.GetActivePlansAsync(cancellationToken);

        return Ok(plans);
    }
}
