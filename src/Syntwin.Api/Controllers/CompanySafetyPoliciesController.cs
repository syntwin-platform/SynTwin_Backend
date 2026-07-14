using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Syntwin.Application.RobotSafety.Dtos;
using Syntwin.Application.RobotSafety.Interfaces;

namespace Syntwin.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/companies/{companyId:guid}/safety-policy")]
public sealed class CompanySafetyPoliciesController : ControllerBase
{
    private readonly IRobotSafetyPolicyService _policyService;

    public CompanySafetyPoliciesController(IRobotSafetyPolicyService policyService)
    {
        _policyService = policyService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(SafetyPolicyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SafetyPolicyResponse>> Get(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        var policy = await _policyService.GetCompanyPolicyAsync(
            userId.Value,
            companyId,
            cancellationToken);

        return policy is null
            ? NotFound(new { message = "Company safety policy not found." })
            : Ok(policy);
    }

    [HttpPut]
    [ProducesResponseType(typeof(SafetyPolicyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SafetyPolicyResponse>> Upsert(
        Guid companyId,
        [FromBody] UpsertSafetyPolicyRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        try
        {
            var policy = await _policyService.UpsertCompanyPolicyAsync(
                userId.Value,
                companyId,
                request,
                GetClientIpAddress(),
                cancellationToken);

            return policy is null
                ? NotFound(new { message = "Company not found." })
                : Ok(policy);
        }
        catch (UnauthorizedAccessException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : null;
    }

    private string? GetClientIpAddress()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}