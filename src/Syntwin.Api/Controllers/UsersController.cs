using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Syntwin.Application.Users.Dtos;
using Syntwin.Application.Users.Interfaces;

namespace Syntwin.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/users")]
public sealed class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserProfileResponse>> Me(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        var response = await _userService.GetProfileAsync(userId.Value, cancellationToken);

        return response is null
            ? NotFound(new { message = "User not found." })
            : Ok(response);
    }

    [HttpPatch("me")]
    public async Task<ActionResult<CurrentUserProfileResponse>> UpdateMe(
        UpdateCurrentUserRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        var response = await _userService.UpdateProfileAsync(userId.Value, request, cancellationToken);

        return response is null
            ? NotFound(new { message = "User not found." })
            : Ok(response);
    }

    [HttpGet("me/subscription")]
    public async Task<ActionResult<CurrentSubscriptionResponse>> CurrentSubscription(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        var response = await _userService.GetCurrentSubscriptionAsync(userId.Value, cancellationToken);

        return response is null
            ? NotFound(new { message = "Subscription not found." })
            : Ok(response);
    }

    [HttpPatch("me/subscription")]
    public ActionResult UpdateSubscription()
    {
        return BadRequest(new
        {
            message = "Use /api/payments/vnpay/checkout to change subscription plan."
        });
    }
    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}