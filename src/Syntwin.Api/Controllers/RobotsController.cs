using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Syntwin.Application.Robots.Dtos;
using Syntwin.Application.Robots.Interfaces;

namespace Syntwin.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/robots")]
public sealed class RobotsController : ControllerBase
{
    private readonly IRobotService _robotService;

    public RobotsController(IRobotService robotService)
    {
        _robotService = robotService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RobotResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<RobotResponse>>> GetMine(
        [FromQuery] Guid? companyId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        var robots = await _robotService.GetMineAsync(
            userId.Value,
            companyId,
            cancellationToken);

        return Ok(robots);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(RobotResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RobotResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        var robot = await _robotService.GetByIdAsync(
            userId.Value,
            id,
            cancellationToken);

        return robot is null
            ? NotFound(new { message = "Robot not found." })
            : Ok(robot);
    }

    [HttpGet("{id:guid}/state/latest")]
    [ProducesResponseType(typeof(RobotLatestStateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RobotLatestStateResponse>> GetLatestState(
    Guid id,
    CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        var state = await _robotService.GetLatestStateAsync(
            userId.Value,
            id,
            cancellationToken);

        return state is null
            ? NotFound(new { message = "Robot not found." })
            : Ok(state);
    }

    [HttpGet("{id:guid}/runtime-config")]
    [ProducesResponseType(typeof(RobotRuntimeConfigResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RobotRuntimeConfigResponse>> GetRuntimeConfig(
    Guid id,
    CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        var runtimeConfig = await _robotService.GetRuntimeConfigAsync(
            userId.Value,
            id,
            cancellationToken);

        return runtimeConfig is null
            ? NotFound(new { message = "Robot not found." })
            : Ok(runtimeConfig);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CreateRobotResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CreateRobotResponse>> Create(
        CreateRobotRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        try
        {
            var response = await _robotService.CreateAsync(
                userId.Value,
                request,
                GetClientIpAddress(),
                cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { id = response.Robot.Id },
                response);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (UnauthorizedAccessException exception)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = exception.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(RobotResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RobotResponse>> Update(
        Guid id,
        UpdateRobotRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        try
        {
            var robot = await _robotService.UpdateAsync(
                userId.Value,
                id,
                request,
                GetClientIpAddress(),
                cancellationToken);

            return robot is null
                ? NotFound(new { message = "Robot not found." })
                : Ok(robot);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (UnauthorizedAccessException exception)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = exception.Message });
        }
    }

    [HttpPut("{id:guid}/scene-binding")]
    [ProducesResponseType(typeof(RobotResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RobotResponse>> UpdateSceneBinding(
    Guid id,
    RobotSceneBindingRequest request,
    CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        try
        {
            var robot = await _robotService.UpdateSceneBindingAsync(
                userId.Value,
                id,
                request,
                GetClientIpAddress(),
                cancellationToken);

            return robot is null
                ? NotFound(new { message = "Robot not found." })
                : Ok(robot);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (UnauthorizedAccessException exception)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = exception.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        try
        {
            var disabled = await _robotService.DisableAsync(
                userId.Value,
                id,
                GetClientIpAddress(),
                cancellationToken);

            return disabled
                ? NoContent()
                : NotFound(new { message = "Robot not found." });
        }
        catch (UnauthorizedAccessException exception)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = exception.Message });
        }
    }

    [HttpPost("{id:guid}/device-secret/reset")]
    [ProducesResponseType(typeof(ResetRobotDeviceSecretResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResetRobotDeviceSecretResponse>> ResetDeviceSecret(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        try
        {
            var response = await _robotService.ResetDeviceSecretAsync(
                userId.Value,
                id,
                GetClientIpAddress(),
                cancellationToken);

            return response is null
                ? NotFound(new { message = "Robot not found." })
                : Ok(response);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (UnauthorizedAccessException exception)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = exception.Message });
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
