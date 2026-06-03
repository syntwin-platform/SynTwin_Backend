using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Syntwin.Application.Admin.Dtos;
using Syntwin.Application.Admin.Interfaces;

namespace Syntwin.Api.Controllers;

[ApiController]
[Authorize(Roles = "SuperAdmin")]
[Route("api/admin/users")]
public sealed class AdminUsersController : ControllerBase
{
    private readonly IAdminUserService _adminUserService;

    public AdminUsersController(IAdminUserService adminUserService)
    {
        _adminUserService = adminUserService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(AdminUserListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AdminUserListResponse>> ListUsers(
        [FromQuery] string? search,
        [FromQuery] string? role,
        [FromQuery] string? status,
        [FromQuery] string? plan,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var users = await _adminUserService.ListUsersAsync(
                search, role, status, plan, page, pageSize, cancellationToken);

            return Ok(users);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AdminUserDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminUserDetailResponse>> GetUser(
        Guid id,
        CancellationToken cancellationToken)
    {
        var user = await _adminUserService.GetUserAsync(id, cancellationToken);

        return user is null
            ? NotFound(new { message = "User not found." })
            : Ok(user);
    }

    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(AdminUserDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminUserDetailResponse>> UpdateStatus(
        Guid id,
        AdminUpdateUserStatusRequest request,
        CancellationToken cancellationToken)
    {
        var adminUserId = GetCurrentUserId();

        if (adminUserId is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        try
        {
            var user = await _adminUserService.UpdateStatusAsync(
                adminUserId.Value, id, request, cancellationToken);

            return user is null
                ? NotFound(new { message = "User not found." })
                : Ok(user);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPatch("{id:guid}/role")]
    [ProducesResponseType(typeof(AdminUserDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminUserDetailResponse>> UpdateRole(
        Guid id,
        AdminUpdateUserRoleRequest request,
        CancellationToken cancellationToken)
    {
        var adminUserId = GetCurrentUserId();

        if (adminUserId is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        try
        {
            var user = await _adminUserService.UpdateRoleAsync(
                adminUserId.Value, id, request, cancellationToken);

            return user is null
                ? NotFound(new { message = "User not found." })
                : Ok(user);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPatch("{id:guid}/subscription")]
    [ProducesResponseType(typeof(AdminUserDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminUserDetailResponse>> UpdateSubscription(
        Guid id,
        AdminUpdateUserSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await _adminUserService.UpdateSubscriptionAsync(
                id,
                request,
                cancellationToken);

            return user is null
                ? NotFound(new { message = "User not found." })
                : Ok(user);
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
}
