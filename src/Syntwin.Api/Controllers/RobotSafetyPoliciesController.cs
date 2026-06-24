using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Syntwin.Application.RobotSafety.Dtos;
using Syntwin.Application.RobotSafety.Interfaces;

namespace Syntwin.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/robots/{robotId:guid}/safety-policy")]
public sealed class RobotSafetyPoliciesController : ControllerBase
{
    private readonly IRobotSafetyPolicyService _policyService;

    public RobotSafetyPoliciesController(IRobotSafetyPolicyService policyService)
    {
        _policyService = policyService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(SafetyPolicyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SafetyPolicyResponse>> Get(
        Guid robotId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        var policy = await _policyService.GetRobotPolicyAsync(
            userId.Value,
            robotId,
            cancellationToken);

        return policy is null
            ? NotFound(new { message = "Robot safety policy not found." })
            : Ok(policy);
    }

    [HttpPut]
    [ProducesResponseType(typeof(SafetyPolicyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SafetyPolicyResponse>> Upsert(
        Guid robotId,
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
            var policy = await _policyService.UpsertRobotPolicyAsync(
                userId.Value,
                robotId,
                request,
                GetClientIpAddress(),
                cancellationToken);

            return policy is null
                ? NotFound(new { message = "Robot not found." })
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

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid robotId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        try
        {
            var deleted = await _policyService.DeleteRobotPolicyAsync(
                userId.Value,
                robotId,
                GetClientIpAddress(),
                cancellationToken);

            return deleted
                ? NoContent()
                : NotFound(new { message = "Robot not found." });
        }
        catch (UnauthorizedAccessException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = exception.Message });
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